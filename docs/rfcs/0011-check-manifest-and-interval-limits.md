---
rfc: 11
title: "Check manifest, config-as-schema, and interval/timeout limits"
status: implemented
created: 2026-07-17
tracking-issue: 188
depends-on: ["0003"]
proposal-pr: null
implementation-pr: 189
---

# RFC 0011 — Check manifest, config-as-schema, and interval/timeout limits

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-17

## 1. Problem

Three related problems, one root cause — and Piro already solved the same root cause once, for Integrations (RFC 0003), so the fix here is mostly **reuse**.

**(A) Per-`CheckType` metadata is scattered across eight+ places and already drifts.** Everything Piro "knows" about a check type is spread across independent sources that must be hand-kept in sync:

| Fact | Where it lives |
|---|---|
| The type exists | `CheckType` enum (`src/Piro.Domain/Enums/CheckType.cs:4-14`) |
| Which `AlertFor`s it supports | `CheckTypeExtensions.AllowedAlertFors` switch (`src/Piro.Domain/Extensions/CheckTypeExtensions.cs:12-23`) |
| Whether it needs an Integration | `[RequiresIntegration]` on the executor (`GcpCloudRunJobCheckExecutor.cs:15`) |
| That it *has* an executor at all | DI registration (`InfrastructureServiceExtensions.cs:119-124`) |
| Its human display name | `CHECK_TYPE_LABELS` in the **frontend** (`apps/admin/src/constants/checks.ts:13-21`) |
| Its config-form defaults | `CHECK_TYPE_DEFAULTS` frontend (`constants/checks.ts:23-31`) |
| Its allowed `AlertFor`s (again) | `ALLOWED_ALERT_FORS` frontend (`constants/checks.ts:35-44`), whose own comment says *"mirrors CheckTypeExtensions.AllowedAlertFors()… Keep both in sync"* |
| Its config-field renderer | `CHECK_TYPE_CONFIG_RENDERERS` frontend (`CheckTypeConfigFields.tsx:10-19`) |

This is not hypothetical drift — it has **already happened**: `CheckType.GRPC` exists in the enum (`CheckType.cs:12`) and in the frontend's `ALLOWED_ALERT_FORS` (`checks.ts:42`), but there is **no `GrpcCheckExecutor`**, **no label** in `CHECK_TYPE_LABELS`, and **no entry** in `CHECK_TYPE_DEFAULTS`. The type set is inconsistent across the very sources meant to agree.

**(B) There is no floor on how often a check runs, and no ceiling relating a check's timeout to its interval.** `Check.Cron` is a free Quartz string (`Check.cs:22-23`, default `"* * * * *"` = every minute) validated only as `cron: z.string()` on the frontend (`validations.ts:59`) — no minimum interval, nothing on the backend. A check can be scheduled every second, and its per-type timeout (`HttpCheckData.TimeoutMs` etc., `HttpCheckData.cs:16`) can exceed its own interval. `[DisallowConcurrentExecution]` (`CheckExecutionJob.cs:10`) stops the *same* check overlapping itself, but a timeout longer than the interval means the check is effectively always running, monopolizing a Quartz worker slot (`MaxConcurrency = ProcessorCount * 2`, `InfrastructureServiceExtensions.cs:136`) and starving other checks. The per-type minimum this needs (a `Script` check floors at 5 min, RFC 0010; ordinary checks at 1 min) is exactly the kind of fact problem (A) says has nowhere to live.

**(C) Each check type's *config shape* is defined once in the backend but re-declared, by hand, three more times in the frontend.** Each type's config is a typed record — `HttpCheckData` (`src/Piro.Application/Models/TypeData/HttpCheckData.cs`), `DnsCheckData`, `TcpCheckData`, `PingCheckData`, `SslCheckData`, `GcpCloudRunJobCheckData` — which the executor deserializes `Check.TypeDataJson` into at run time (`HttpCheckExecutor.cs:27-28`). That record is the canonical shape, but it is used **only** on the backend. The frontend re-expresses the same shape by hand three times: field **defaults** (`CHECK_TYPE_DEFAULTS`, `constants/checks.ts:23-31`), field **layout & controls** (a bespoke React component per type — `HttpConfig.tsx`, `DnsConfig.tsx`, … wired via `CHECK_TYPE_CONFIG_RENDERERS`, `CheckTypeConfigFields.tsx:10-19`), and field **validation** (per-type `superRefine` in `validations.ts`). Four edits for one conceptual field, nothing enforcing agreement. There is also **no field-level description** surfaced to the operator (only sparse `///` doc-comments), and **no description of what a check type does** ("HTTP: fetch a URL and assert on the response") anywhere.

**The root cause is shared: the backend holds the truth about check types — their metadata and their config shape — but that truth is neither centralized nor exposed, so it is re-declared and drifts.** Integrations hit this exact problem and solved it in RFC 0003 with a **manifest attribute + reflected config-schema + a generic dynamic form** (`IntegrationManifestAttribute`, `IntegrationManifestExtensions.BuildConfigSchema`, `DynamicConfigField.tsx`). This RFC does the same for checks by **reusing that engine's already-generic primitives** and extending them with the field types checks need that notifications didn't.

## 2. Non-goals

- **A new, parallel config-form engine.** This RFC explicitly does **not** build a checks-specific form engine. It reuses the existing `ConfigField*` attributes, `ConfigFieldType`, `ConfigFieldSchemaDto`, the reflection builder, and `DynamicConfigField.tsx` from RFC 0003 (§4.5). Inventing a second engine when a generic one exists would be the anti-pattern this whole design avoids.
- **Making `CheckType` extensible at runtime / a plugin system.** The manifest is a static, in-code registry keyed by the `CheckType` enum; types are still added in code. Same posture RFC 0003 §7 took for `IntegrationType`.
- **Rewriting existing sub-minimum checks.** The interval floor is enforced on **new writes only** (create/update); pre-existing sub-floor checks keep running until next edited (§4.6). No migration mutates operator schedules.
- **A general per-job wall-clock deadline in the dispatcher.** As in RFC 0010 §2, each executor owns its own timeout; this RFC only *validates the relationship* between timeout and interval.
- **Changing what `AlertFor`s a type supports.** The manifest *absorbs* `AllowedAlertFors` byte-for-byte (RFC 0002's concern is untouched).
- **Sub-minute checks.** The global floor is 1 minute; this removes silent sub-minute scheduling rather than adding finer granularity.
- **Normalizing per-type timeout out of `TypeDataJson` into a `Check` column.** Tempting (it would make "timeout < interval" trivial), but it is a broader schema change touching every executor and only 3 of 7 types have a timeout — out of scope (§7).

## 3. Design principle

**Reuse the Integrations config-schema engine (RFC 0003) as the shared mechanism for checks; a per-`CheckType` manifest becomes the single source of truth for type metadata, the `*CheckData` records become self-describing via the same annotations, and interval/timeout validation is one more consumer of the manifest.** Everything below traces to this: the manifest mirrors `IntegrationManifestAttribute` for `CheckType` (§4.1); the reflection builder that emits `ConfigFieldSchemaDto[]` is *extracted* from `IntegrationManifestExtensions` into a type-agnostic helper both features call (§4.2); the generic `DynamicConfigField` renderer is *extended* (not forked) with the field types checks need (§4.3); the `*CheckData` records are *annotated* rather than re-described in the frontend (§4.4); and interval/timeout validation reads the floor from the manifest in the one place check writes are already validated (§4.5).

## 4. Design

```
  ┌──────────────────────────────────────────────────────────────────────┐
  │  CheckTypeManifest  (Domain, keyed by CheckType)  — mirrors            │
  │  IntegrationManifestAttribute:                                         │
  │    DisplayName · Description · MinInterval · AllowedAlertFors ·         │
  │    RequiredIntegration · ConfigType (the *CheckData record)            │
  └──────────────────────────────────────────────────────────────────────┘
        │ ConfigType                       │ metadata
        ▼                                  ▼
  ┌─────────────────────────┐      AllowedAlertFors  ·  CheckTypesController
  │  ConfigSchemaBuilder     │      (§4.5 lookups)    ·  (serves manifest DTO)
  │  (EXTRACTED from         │                                   │
  │  IntegrationManifest-    │                                   ▼
  │  Extensions — shared by  │      CheckAppService: interval ≥ manifest.MinInterval
  │  Integrations + Checks)  │                       timeoutMs(typeData) < interval
  │  reflects [ConfigField],  │                       interval ≥ 1min global floor
  │  [ConfigFieldOptions],   │
  │  Number/Bool/List/       │  ← EXTENDED ConfigFieldType (§4.3)
  │  KeyValue/ObjectArray/   │
  │  Code (new)              │
  └─────────────────────────┘
        │ ConfigFieldSchemaDto[]  (same wire DTO Integrations already serves)
        ▼
  GET /api/v1/checks/types  ──▶  frontend DynamicConfigField (EXTENDED renderer)
                                  renders the whole form generically; the per-type
                                  *Config.tsx components are retired (§4.4)
```

### 4.1 The check manifest — mirror of the integration manifest

Integrations declare everything about a type in one `IntegrationManifestAttribute` (`src/Piro.Domain/Attributes/IntegrationManifestAttribute.cs`) carrying `Label`, `Description`, `Category`, `Capabilities`, and crucially a **`ConfigType`** pointing at the C# class describing its config shape. Checks get the direct analogue, keyed by `CheckType`:

```csharp
// src/Piro.Domain/Checks/CheckTypeManifest.cs
public sealed record CheckTypeInfo(
    CheckType Type,
    string DisplayName,               // "GCP Cloud Run Job" — was frontend CHECK_TYPE_LABELS
    string Description,               // NEW — "Fetch a URL and assert on the response." Shown in the type picker.
    TimeSpan MinInterval,             // schedule floor (§4.5)
    AlertFor[] AllowedAlertFors,      // absorbs CheckTypeExtensions.AllowedAlertFors
    Type ConfigType,                  // the *CheckData record — feeds the config schema (§4.2)
    IntegrationType? RequiredIntegration = null); // absorbs [RequiresIntegration]

public static class CheckTypeManifest
{
    private static readonly Dictionary<CheckType, CheckTypeInfo> _byType = new[]
    {
        new CheckTypeInfo(CheckType.HTTP, "HTTP",
            "Fetch a URL and assert on the status code and response body.",
            TimeSpan.FromMinutes(1), [AlertFor.Status, AlertFor.Latency], typeof(HttpCheckData)),
        new CheckTypeInfo(CheckType.DNS, "DNS",
            "Resolve a hostname and assert on the returned records.",
            TimeSpan.FromMinutes(1), [AlertFor.Status, AlertFor.Latency, AlertFor.FailedNameServers], typeof(DnsCheckData)),
        new CheckTypeInfo(CheckType.TCP, "TCP",
            "Open a TCP connection to a host and port.",
            TimeSpan.FromMinutes(1), [AlertFor.Status, AlertFor.Latency], typeof(TcpCheckData)),
        new CheckTypeInfo(CheckType.Ping, "Ping",
            "Send an ICMP echo to a host.",
            TimeSpan.FromMinutes(1), [AlertFor.Status, AlertFor.Latency], typeof(PingCheckData)),
        new CheckTypeInfo(CheckType.SSL, "SSL",
            "Check a TLS certificate's validity and expiry.",
            TimeSpan.FromMinutes(1), [AlertFor.Status, AlertFor.CertExpiry], typeof(SslCheckData)),
        new CheckTypeInfo(CheckType.GCP_CloudRunJob, "GCP Cloud Run Job",
            "Verify a Cloud Run Job has completed within a freshness window.",
            TimeSpan.FromMinutes(1), [AlertFor.Status], typeof(GcpCloudRunJobCheckData),
            RequiredIntegration: IntegrationType.GoogleCloud),
        new CheckTypeInfo(CheckType.Script, "Script",
            "Run a sandboxed JavaScript script that returns a status and message.",
            TimeSpan.FromMinutes(5), [AlertFor.Status], typeof(ScriptCheckData)),  // RFC 0010
    }.ToDictionary(i => i.Type);

    public static CheckTypeInfo For(CheckType type) =>
        _byType.TryGetValue(type, out var info) ? info
            : throw new NotSupportedException($"No manifest entry for CheckType.{type}.");

    public static IReadOnlyCollection<CheckTypeInfo> All => _byType.Values;
}
```

It lives in the **Domain** (its facts are domain rules; both `CheckTypeExtensions` in Domain and `CheckAppService` in Application must read it). A `CheckType` with no entry throws from `For(...)`, turning the current silent `GRPC` half-existence into a loud, must-decide error (§8). `RequiredIntegration` moves here from the `[RequiresIntegration]` attribute (only ever read reflectively by `CheckTypesController:26-28`).

Why an in-code manifest rather than a DB table: identical reasoning to RFC 0003 §7 — check types are compiled (each needs an `ICheckExecutor`), so their metadata is code-time knowledge; a table would let the two diverge.

### 4.2 Config-as-schema — extract the reflection builder, reuse the primitives

RFC 0003 already built the reflection engine that turns an annotated config class into a wire schema. Its **primitives are already integration-agnostic** and reused directly:

- **Annotations** — `ConfigFieldAttribute` (`Label`/`Placeholder`/`HelpText`), `ConfigFieldOptionsAttribute`, `MultilineFieldAttribute`, `SecretFieldAttribute`, `GeneratedFieldAttribute`, `SupportsFileUploadAttribute` — all in `Piro.Domain.Attributes`, none referencing any Integration type (their XML docs mention integrations, the code does not). Reused **as-is** on the `*CheckData` records.
- **Wire DTO** — `ConfigFieldSchemaDto` (`src/Piro.Application/DTOs/IntegrationTypeMetaDto.cs:9-21`) depends only on `ConfigFieldType`. Reused **as-is**.
- **The reflection builder** — `BuildConfigSchema`/`BuildFieldSchema`/`InferFieldType` + the `SchemaCache` (`IntegrationManifestExtensions.cs:67-117`, cache at `:22-25` per RFC 0003 §8). These operate purely on a `Type`/`PropertyInfo`; only the *entry point* `ToMetaDto` is integration-specific. **Refactor:** extract them into a type-agnostic `ConfigSchemaBuilder.For(Type configType) → IReadOnlyList<ConfigFieldSchemaDto>` (with the same static cache) that **both** `IntegrationManifestExtensions.ToMetaDto` and the new checks path call. This is a pure extraction — Integrations' behavior is unchanged, it just calls the shared helper.

The `ConfigType` on each manifest entry (§4.1) is fed to this shared builder to produce the check's config schema.

### 4.3 Extending `ConfigFieldType` for the field kinds checks need

The shipped `ConfigFieldType` (`src/Piro.Domain/Enums/ConfigFieldType.cs:11-18`) is `String, Url, Email, Enum, Multiline` — scalar-only, because notification configs are flat (API keys, URLs, tokens). The real `*CheckData` records need controls notifications never did:

| Check field (real) | Needs | In enum today? |
|---|---|---|
| `Url`, `Host` (`HttpCheckData`, `DnsCheckData`…) | text | ✅ `String`/`Url` |
| `Method`, `RecordType` | select | ✅ `Enum` (via `[ConfigFieldOptions]`) |
| `Port`, `TimeoutMs`, `MaxAgeHours` | **number** | ❌ add `Number` |
| `FollowRedirects` | **checkbox** | ❌ add `Boolean` |
| `Headers` (`Dictionary<string,string>`) | **key/value repeater** | ❌ add `KeyValue` |
| `NameServers`, `ExpectedStatusCodes` (`List<string>`) | **string list** | ❌ add `StringList` |
| `ResponseRules` (`List<HttpResponseRule>`) | **repeater of nested objects** | ❌ add `ObjectArray` |
| `Script` (`ScriptCheckData`, RFC 0010) | code editor | ❌ add `Code` |

So the enum gains: `Number`, `Boolean`, `StringList`, `KeyValue`, `ObjectArray`, `Code`. Correspondingly:

- **`InferFieldType`** (the extracted builder, §4.2) learns to map CLR types: `int`/`long`/`double` → `Number`; `bool` → `Boolean`; `List<string>` → `StringList`; `Dictionary<string,string>` → `KeyValue`; `List<T>` where `T` is a record → `ObjectArray`; a `[Code]` marker (new, one-line attribute) → `Code`. Explicit annotations still win (`[ConfigFieldOptions]` → `Enum`, `[Multiline]` → `Multiline`), preserving RFC 0003's precedence.
- **`ObjectArray` is recursive.** `ConfigFieldSchemaDto` gains an optional `ItemSchema: IReadOnlyList<ConfigFieldSchemaDto>?` — for an `ObjectArray` field the builder recurses into `T`'s properties (e.g. `HttpResponseRule`'s `Type`/`Value`/`Expected`/`Degraded`) and nests their schema. This is the one genuinely new mechanism; everything else is additive enum values.
- **`DynamicConfigField.tsx`** (`apps/admin/src/features/integrations/components/`) is **extended**, not forked: today it switches on `Enum`/`Multiline`/else (`:70-95`). It gains branches for `Number` (numeric input), `Boolean` (checkbox), `StringList` (add/remove text rows), `KeyValue` (add/remove key+value rows — the same shape `HttpConfig.tsx` hand-rolls today), `Code` (the CodeMirror editor from RFC 0010 §4.7), and `ObjectArray` (a repeater that renders `field.itemSchema` recursively via `DynamicConfigField` itself, with add/remove). Because Integrations never emits the new types, its rendering is unchanged.

Since the renderer is currently under `features/integrations/`, the shared pieces (`DynamicConfigField`, `GeneratedConfigField`, the config-form scaffolding) move to a neutral location (e.g. `components/config-form/`) and both features import them — a move/re-export, not a rewrite (the components are already generically named and integration-agnostic in body; only their import path is integration-namespaced).

### 4.4 Annotating the `*CheckData` records; retiring the per-type forms

Each `*CheckData` record gets `[ConfigField]`/`[ConfigFieldOptions]`/`[Multiline]`/`[Code]` annotations — the field's default stays where it already is (the record's initializer), so there is **one** source for defaults, not a separate `CHECK_TYPE_DEFAULTS`. Example:

```csharp
public record HttpCheckData
{
    [ConfigField("URL", HelpText = "The URL to request."), Required]
    public string Url { get; init; } = "";

    [ConfigField("Method"), ConfigFieldOptions("GET", "POST", "HEAD", "PUT", "DELETE")]
    public string Method { get; init; } = "GET";

    [ConfigField("Headers", HelpText = "Sent with the request.")]
    public Dictionary<string, string>? Headers { get; init; }        // → KeyValue

    [ConfigField("Timeout (ms)", HelpText = "Abort after this many milliseconds.")]
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; init; } = 5000;                       // → Number

    [ConfigField("Follow redirects")]
    public bool FollowRedirects { get; init; } = true;               // → Boolean

    [ConfigField("Response rules", HelpText = "Assertions on the body; first failure wins.")]
    public List<HttpResponseRule>? ResponseRules { get; init; }      // → ObjectArray (nested)
}
```

With the schema served (§4.5), the frontend renders the whole config form from `DynamicConfigField`. The per-type `*Config.tsx` components (`HttpConfig.tsx`, `DnsConfig.tsx`, …) and the `CHECK_TYPE_CONFIG_RENDERERS` map (`CheckTypeConfigFields.tsx:10-19`) are **retired**, along with the frontend `CHECK_TYPE_DEFAULTS`/`CHECK_TYPE_LABELS`/`ALLOWED_ALERT_FORS` mirror tables (§4.7). This is exactly the follow-up RFC 0003 forecast in its own §6 step 5 ("replace the hardcoded `*Config.tsx` components with one renderer driven by the reflected `ConfigFieldSchema[]`") — for integrations it stopped at the mechanism; here it is realized for checks and, once the renderer is generalized, available to retrofit integrations too.

One hand-tuned concern survives generically: `HttpCheckData`'s `ExpectedStatusCodes` accepts both `"200"` and legacy integer `200` via `StatusCodeListConverter` (`HttpCheckData.cs:34-58`). That is a *deserialization* nicety, orthogonal to the *form* — the form renders it as a `StringList`; the converter keeps accepting legacy JSON on read. No conflict.

### 4.5 Backend consumers and interval/timeout validation

**Metadata consumers collapse to manifest lookups:**
- `CheckTypeExtensions.AllowedAlertFors` (`CheckTypeExtensions.cs:12-23`) becomes `type => CheckTypeManifest.For(type).AllowedAlertFors`; the `switch`/`throw` is deleted.
- `CheckTypesController` (`CheckTypesController.cs:14-36`) stops reflecting executor attributes and projects the manifest — including each type's `ConfigSchema` (from §4.2) and `Description` — into a widened `CheckTypeMetaDto` (§5). It still injects `IEnumerable<ICheckExecutor>` only to report `HasExecutor` (so a manifested-but-unrunnable type like `GRPC` is representable, not a throw).

**Interval/timeout validation** — a new guard in `CheckAppService`, sibling of the existing `EnsureAlertForAllowed` (`CheckAppService.cs:114-118`), called from `CreateAsync` (after the alert-for loop, ~`:61`) and `UpdateAsync` (after `:130`). `DomainValidationException` is the established error type.

```csharp
private static void EnsureScheduleWithinBounds(CheckType type, string cron, string typeDataJson)
{
    var interval = CronInterval(cron);                       // smallest gap over a sampling window
    var min = CheckTypeManifest.For(type).MinInterval;
    if (interval < TimeSpan.FromMinutes(1))
        throw new DomainValidationException("Check interval must be at least 1 minute.");
    if (interval < min)
        throw new DomainValidationException($"{type} checks must run no more often than every {min.TotalMinutes:0} minutes.");
    if (TimeoutFromTypeData(type, typeDataJson) is { } t && t >= interval)
        throw new DomainValidationException($"Timeout ({t}) must be shorter than the check interval ({interval}).");
}
```

Two codebase facts shape it: `UpdateAsync` has no `request.Type` (type is immutable on update), so the min lookup reads `check.Type` off the loaded entity; and `timeoutMs` is **not uniform** — only `HttpCheckData:16`, `TcpCheckData:12`, `PingCheckData:11` have it (DNS/SSL/GCP have none, Script has its whole-script `TimeoutMs`, RFC 0010 §5) — so `TimeoutFromTypeData` deserializes the type-specific model and returns `null` when absent, in which case only the interval floors apply.

**Deriving the interval from a cron.** Piro has no interval concept today and no cron-parsing lib beyond Quartz (no Cronos/NCrontab). The validator uses Quartz's own `CronExpression` (available transitively via the `Quartz` package): build it from `ToQuartzCron(cron)` — the same 5→6-field conversion `CheckSchedulerService.ToQuartzCron` already does (`:73-83`) — take `GetNextValidTimeAfter` twice, and use the **smallest** gap over a sampling window (an irregular cron like `0 0 9,17 * * ?` has unequal gaps; the floor must hold against the tightest). A small `CronInterval` helper lands beside `ToQuartzCron`, reused by scheduler and validator so their cron interpretation can't diverge.

### 4.6 Legacy sub-minimum checks

Enforced **on create/update only**, never retroactively. A check already below the floor keeps running on its current cron until someone edits it — then `EnsureScheduleWithinBounds` runs and the save fails until the cron is raised. No migration rewrites schedules; introducing the floor breaks nothing in flight. Same permissive-then-enforce posture as RFC 0008's governance.

### 4.7 Frontend — the manifest + schema replace the mirror tables

With the widened `CheckTypeMetaDto` served by `GET /api/v1/checks/types`, the frontend reads from the API what it hand-mirrors today:
- **Config form** — rendered entirely by the extended `DynamicConfigField` (§4.3) from `meta.configSchema`; the per-type `*Config.tsx` and `CHECK_TYPE_CONFIG_RENDERERS` are deleted (§4.4). Defaults come from the schema (each field's default), so `CHECK_TYPE_DEFAULTS` is deleted.
- **Display name & description** — `CHECK_TYPE_LABELS` deleted; the type dropdown (`CheckFormPage.tsx:118,122`) and detail header (`CheckDetailPage.tsx:146,291,316`) use `meta.displayName`, and the picker shows `meta.description` (new).
- **Allowed alert-fors** — `ALLOWED_ALERT_FORS` and its "keep in sync" comment deleted; the form reads `meta.allowedAlertFors`.
- **Interval presets & floor** — `CRON_PRESETS` (`constants/checks.ts:3-11`) stays as the option list, but `CheckGeneralSettingsFields.tsx` (`:94-105`) hides presets below `meta.minIntervalSeconds` (a `Script` check never offers "every minute"), and the custom-cron branch gets a `superRefine` floor in `validations.ts` mirroring the backend. Timeout-vs-interval is validated client-side too, sourced from the schema's timeout field.

### 4.8 What does NOT change

- **The RFC 0003 config engine's contract for Integrations.** Extracting `ConfigSchemaBuilder` (§4.2) and moving `DynamicConfigField` to a neutral folder (§4.3) are refactors; `IntegrationManifestExtensions.ToMetaDto`, `IntegrationTypeMetaDto`, and the integration form's behavior are byte-for-byte the same. Integrations never emit the new field types, so their rendering path is untouched.
- **`ConfigFieldSchemaDto`'s existing fields, and the annotation attributes' meaning.** Additive only: the DTO gains an optional `ItemSchema`; the enum gains values; no existing field/value changes.
- **The `CheckType` enum, `ICheckExecutor`, executor registration, and scheduling.** The manifest is keyed by the enum and orthogonal to executors. `CheckSchedulerService.ScheduleAsync`/`ToQuartzCron` are reused; validation runs before scheduling, so the scheduler never sees an out-of-bounds cron.
- **`AlertFor` semantics** (RFC 0002) and **`Check.TypeDataJson` storage** — the manifest relocates the type→fors mapping and describes the config shape; it changes neither the alert model nor how config is stored/deserialized.
- **Existing checks' runtime behavior** — no migration, no reschedule (§4.6).

## 5. Data / schema scope

- **No database changes.** Manifest is in-code; config-schema is reflected at request time (cached); interval rules are validation. `Check.Cron`/`Check.TypeDataJson` unchanged.
- **New Domain:** `CheckTypeManifest.cs` + `CheckTypeInfo` (`src/Piro.Domain/Checks/`); `CodeFieldAttribute` marker (`Piro.Domain.Attributes`); new `ConfigFieldType` values `Number, Boolean, StringList, KeyValue, ObjectArray, Code` (`src/Piro.Domain/Enums/ConfigFieldType.cs`).
- **New shared Application helper:** `ConfigSchemaBuilder` (extracted from `IntegrationManifestExtensions`), with the recursion for `ObjectArray` and the extended `InferFieldType`.
- **Changed DTOs:** `ConfigFieldSchemaDto` gains optional `ItemSchema` (`IntegrationTypeMetaDto.cs`); `CheckTypeMetaDto` (`src/Piro.Application/DTOs/CheckTypeMetaDto.cs`) widens from `(Type, RequiredIntegrationType)` to add `DisplayName`, `Description`, `MinIntervalSeconds`, `AllowedAlertFors`, `ConfigSchema` (`ConfigFieldSchemaDto[]`), `HasExecutor`.
- **Changed backend:** `CheckTypeExtensions.AllowedAlertFors` (manifest delegate); `CheckTypesController` (projects manifest + schema); `CheckAppService` (`EnsureScheduleWithinBounds` in create/update); `IntegrationManifestExtensions` (calls extracted builder). `*CheckData` records gain annotations.
- **Removed:** `[RequiresIntegration]` usage (superseded by manifest field); frontend `CHECK_TYPE_LABELS`, `CHECK_TYPE_DEFAULTS`, `ALLOWED_ALERT_FORS`, `CHECK_TYPE_CONFIG_RENDERERS`, and all `check-types/*Config.tsx`; regenerated API types (`pnpm run generate:api-types`).
- **Moved (not rewritten):** `DynamicConfigField.tsx`/`GeneratedConfigField.tsx` + form scaffolding → `apps/admin/src/components/config-form/`.
- **No new NuGet/npm dependency** — `Quartz.CronExpression` ships in the referenced `Quartz` package; CodeMirror already arrives via RFC 0010.

## 6. Phased plan

1. **Extract `ConfigSchemaBuilder` + extend `ConfigFieldType` (backend).** Pull `BuildConfigSchema`/`BuildFieldSchema`/`InferFieldType`/cache out of `IntegrationManifestExtensions` into a shared helper (Integrations now calls it — behavior identical); add the new enum values, the `ObjectArray` recursion + `ItemSchema` on the DTO, and CLR-type inference for Number/Boolean/StringList/KeyValue/Code. Independently shippable: Integrations keep working, new types are just unused so far.
2. **Extend `DynamicConfigField` + move to shared folder (frontend).** Add the Number/Boolean/StringList/KeyValue/Code/ObjectArray branches (ObjectArray recursing into `itemSchema`); relocate the shared components to `components/config-form/`; Integrations re-import from there. Verifiable against existing integration forms (no visual change) before any check uses it.
3. **Check manifest + metadata consumers (backend).** `CheckTypeManifest`/`CheckTypeInfo`, redirect `AllowedAlertFors`, widen `CheckTypeMetaDto` (incl. `ConfigSchema` from Phase 1, `Description`), reproject `CheckTypesController`, delete `[RequiresIntegration]`. Decide `GRPC`'s fate (§8). Kills the metadata drift.
4. **Annotate `*CheckData` + interval/timeout validation (backend).** Add `[ConfigField]`/etc. to the six records; add `CronInterval` + `EnsureScheduleWithinBounds` reading `MinInterval` from the manifest. This is the phase RFC 0010's `Script` floor depends on.
5. **Frontend cutover.** Render check config from `meta.configSchema` via the extended renderer; delete `*Config.tsx`, `CHECK_TYPE_CONFIG_RENDERERS`, and the mirror tables; source display name/description/alert-fors from the API; filter presets by `minIntervalSeconds`. Depends on 2–4.
6. **(Optional) Retrofit Integrations to the extended renderer.** Now that the renderer is generalized, integration configs could adopt any new field types they'd benefit from — not required, listed so the generalization's payoff is explicit.

## 7. Alternatives considered

- **Build a checks-specific config-form engine.** Rejected — RFC 0003 already built a generic, reflection-driven one whose primitives (`ConfigField*` attributes, `ConfigFieldType`, `ConfigFieldSchemaDto`, the builder, `DynamicConfigField`) are integration-agnostic by name and namespace (verified: none reference an Integration type). A second engine would duplicate the exact thing 0003's own §6 step 5 forecast reusing. The cost here is *extending* one engine (new field types) and *extracting* one shared helper, not building anew.
- **Leave metadata scattered; just add a min-interval check.** Rejected — the min-interval *is* per-type metadata, so it becomes a ninth scattered site. The `GRPC` inconsistency proves scattering already fails.
- **A DB-backed manifest / config schema.** Rejected for the same reason RFC 0003 §7 rejected it: check types are compiled (each needs an executor), so their metadata is code-time truth; a table invites a row with no executor or vice-versa.
- **Keep `AllowedAlertFors`/defaults where they are, centralize only the new facts.** Rejected — it leaves the frontend's confessed "keep in sync" duplication (`checks.ts:44`) in place. If a manifest exists, these are exactly what it should own.
- **Full nested `ObjectArray` is too much for v1 — hand-code `ResponseRules`.** Rejected (per decision to include it) — leaving one field type bespoke means the HTTP form is still half-generic/half-custom, re-creating the maintenance split the RFC removes. The recursion is bounded (a nested schema is just another `ConfigFieldSchemaDto[]`) and the renderer already recurses by calling itself.
- **Enforce the interval floor in `CheckSchedulerService` (schedule time) not `CheckAppService` (write time).** Rejected — scheduling runs after persistence, catching violations too late for a clean API error. `CheckAppService` is where every other check invariant lives and has `Type`+`Cron`+`TypeDataJson` in hand.
- **A migration raising sub-minimum legacy checks to the floor.** Rejected — mutates operator schedules silently; "everything now runs less often" is a surprising SLA-affecting change. Enforce-on-next-edit (§4.6) reaches the same end state without a disruptive rewrite.
- **Per-type timeout as a first-class `Check` column.** Considered, out of scope (§2) — broader schema change across every executor for a rule this RFC can enforce by deserializing where the timeout currently lives.

## 8. Risks

- **Deriving an interval from an arbitrary cron is approximate for irregular schedules.** `0 0 9,17 * * ?` has 8h/16h gaps — "the interval" isn't one number. Mitigation: validate against the **minimum** gap in a sampling window (§4.5). Residual: a cron with one tiny gap a year is over-restricted — rare; handle by allowing custom crons to bypass with a warning if it ever surfaces.
- **The manifest's missing-entry throw activates the latent `GRPC` inconsistency.** The first `CheckTypeManifest.For(GRPC)` throws where today the frontend silently lacks a label. Intended (loud > silent), but Phase 3 **must decide `GRPC`**: give it a manifest entry with `HasExecutor=false`, or remove it from the enum. The `HasExecutor` flag exists precisely so a manifested-but-unrunnable type is representable without throwing.
- **The `ObjectArray` recursive renderer is the one genuinely new, non-trivial piece.** A nested repeater (add/remove `HttpResponseRule` rows, each with its own fields, some conditional like `Expected` only for `json_path`/`xml_path`) is more complex than any current integration field. Mitigation: it is its own phase (§6 Phase 2), verifiable against the existing hand-coded `HttpConfig.tsx` behavior before that component is deleted; conditional-field logic (show `Expected` only for certain `Type`s) may need a lightweight `[VisibleWhen]`-style annotation, called out as the sharpest sub-risk — if it proves too much for v1, `ResponseRules` can stay bespoke *temporarily* while every other field goes generic, without blocking the rest.
- **Widening `CheckTypeMetaDto` and moving shared components are coordinated breaking changes.** Generated types (`pnpm run generate:api-types`) and the integration form's import paths must update together. Mitigation: DTO changes are additive; the component move is a re-export; phased so integrations are re-verified (Phase 2) before checks depend on the renderer (Phase 5).
- **Frontend/backend interval rules duplicated.** The client `superRefine` floor is a second implementation. Mitigation: it is a UX pre-check; the backend `EnsureScheduleWithinBounds` is authoritative, and the floor *value* comes from the API (`meta.minIntervalSeconds`) — only the comparison is duplicated, not the data.
- **Extraction regressions in the shared builder.** Pulling `BuildConfigSchema`/`InferFieldType` out of `IntegrationManifestExtensions` risks subtly changing integration schema output (naming policy, precedence). Mitigation: the extraction is behavior-preserving by construction (same methods, same cache), and Phase 1 ships with integration forms as the regression oracle — if an integration field renders differently, the extraction is wrong.
```
