---
rfc: 3
title: "Integration manifest"
status: implemented
created: 2026-07-15
depends-on: []
proposal-pr: null
implementation-pr: 172
---

# RFC 0003 — Integration manifest

Status: Implemented
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-15

## 1. Problem

`IntegrationType` (`IntegrationType.cs:5-57`) is a flat enum with 14 values, and nothing declares *what a type actually does* in one place. That knowledge is scattered and partial:

- `IntegrationCategoryAttribute` marks `ThirdParty` vs `Notification` (+ `ChannelOnly`), but says nothing about capabilities within a category.
- `RequiresIntegrationAttribute` (`RequiresIntegrationAttribute.cs:11-14`), used exactly once (`GcpCloudRunJobCheckExecutor.cs:15`), declares a `CheckType` → `IntegrationType` dependency, exposed via `CheckTypesController` — but only for that one relationship.
- `INotificationDispatcher` registration (`InfrastructureServiceExtensions.cs:203-213`) is the real source of truth for "does this type send notifications," but it's implicit: an `IntegrationType` either has a registered dispatcher or it doesn't (`PagerDuty` has none despite not being `[Obsolete]` — a real state today: declared as a live type, no dispatcher wired up).
- `IntegrationExtensions.SecretKeysByType` (`IntegrationExtensions.cs:16-27`) lists *secret* config keys for masking, but not the full `ConfigJson` shape — the actual schema (e.g. Jira's `baseUrl`/`email`/`apiToken`/`projectKey`/`issueType`) only exists hardcoded in `apps/admin`'s per-type React components (`IntegrationFormPage.tsx`'s `buildConfigJson`, lines 35-46, plus 13 separate `*Config.tsx` files).

Adding [RFC 0001](0001-third-party-alert-ingestion.md)'s `GcpCloudMonitoringWebhook` — an *inbound* type, the first one that isn't `ThirdParty`-outbound or `Notification`-outbound — makes the gap concrete: there's no field today that says "this Integration receives things" vs. "this Integration sends things," and no single place that says what it produces when it does (an `Alert`? escalation config? nothing?).

## 2. Non-goals

- **Package-based extensibility** (loading third-party `IntegrationType`s from an external assembly/plugin at runtime) — the long-term motivation for a manifest, but not this RFC. This RFC only makes the *existing*, compiled-in `IntegrationType`s self-describing. Nothing here assumes or requires dynamic loading; that would need its own RFC once there's a real packaging need.

  This is deliberate, not just "not yet gotten to": `Integration.Type` (`Integration.cs:9`) is a C# `enum` persisted as its ordinal `int` in Postgres, and `IntegrationType.cs`'s obsolete values are explicitly kept in place (not removed/renumbered) specifically to avoid breaking those stored ordinals — the whole codebase (dispatcher registration, this RFC's manifest lookup, `CheckTypesController`, `apps/admin`'s `IntegrationFormPage.tsx`) is built assuming a closed, compile-time-known set of types. Reflection over compile-time-known classes (§4.3, for `ConfigType`) is safe and doesn't touch that assumption. Actual dynamic discovery (scanning loaded assemblies for annotated classes at startup, resolving `IntegrationType` as a string identifier instead of an enum) would require migrating that column and every closed `switch`/`Dictionary<IntegrationType, X>` in the codebase to an open-ended lookup — a real, deliberate migration with its own RFC (versioned manifest schema, sandboxing, discovery, the `enum`→`string` data migration), not a side effect of this one.
- **Schema-driven admin forms** — the manifest's `ConfigSchema` (§4.3) is *shaped* so this becomes possible later, but this RFC doesn't rewrite `apps/admin`'s 13 hardcoded `*Config.tsx` components into a generic renderer. That's worthwhile follow-up work, not a requirement to land the manifest itself.
- **Changing any existing `IntegrationType`'s runtime behavior.** This RFC only adds declarative metadata read by new call sites (§4.4); it doesn't touch `INotificationDispatcher`, `ICheckExecutor`, or any existing dispatcher's logic.

## 3. Design principle

**Describe what already exists; don't invent new behavior.** Every fact the manifest declares for an existing `IntegrationType` (has a dispatcher? category? config keys?) is already true today, just undeclared or scattered across three attributes and a frontend switch statement. The manifest consolidates what's real, in one place, machine-readable — it doesn't add capabilities to types that don't have them.

## 4. Design

### 4.1 `IntegrationManifest` — one static declaration per `IntegrationType`

A new `IntegrationManifestAttribute`, applied to a static registration point per type (see §4.2 for where), consolidating what `RequiresIntegrationAttribute` + the implicit dispatcher-registration signal do separately today. **`IntegrationCategoryAttribute` (`ThirdParty` vs `Notification`) is kept as-is, not folded in** — it already answers a real, distinct, useful question ("is this a service/action integration or a notification channel?") and every existing caller of it keeps working unchanged. The manifest answers a finer-grained question (direction + concrete capabilities), layered alongside category, not replacing it:

```csharp
public sealed record IntegrationManifest(
    IntegrationType Type,
    IntegrationDirection Direction,       // Inbound | Outbound | Both
    IntegrationCapability[] Capabilities, // flags — see §4.2
    Type ConfigType                       // typeof(TConfig) — see §4.3
);
```

`IntegrationDirection` is genuinely new information `IntegrationCategoryAttribute` doesn't carry today: `ThirdParty` covers both outbound-action types (`GoogleCloud`, `Jira`) and, as of RFC 0001, an inbound one (`GcpCloudMonitoringWebhook`) — category alone can't tell those apart. `IntegrationDirection` (`Inbound` | `Outbound` | `Both`) is the missing axis, kept as its own field precisely so category keeps meaning what it already means ("service/action integration" vs. "notification channel") without being overloaded to also answer "which way does data flow." A type has both: `GoogleCloud` is `ThirdParty` + `Outbound`; `GcpCloudMonitoringWebhook` is `ThirdParty` + `Inbound`; `Twilio` is `Notification` + `Outbound`.

### 4.2 `IntegrationCapability` — consolidating two separate signals into one flag set

```csharp
[Flags]
public enum IntegrationCapability
{
    None = 0,
    SendsPersonalNotification = 1 << 0, // has a registered INotificationDispatcher
    RequiredByCheckType       = 1 << 1, // some ICheckExecutor needs this type (was RequiresIntegrationAttribute)
    CreatesAlerts             = 1 << 2, // inbound webhook that produces Alert rows (RFC 0001's GcpCloudMonitoringWebhook)
    SupportsEscalationPolicy  = 1 << 3, // Integration.EscalationPolicyId is meaningful for this type (RFC 0001 §4.3)
    SupportsCheckCorrelation  = 1 << 4, // inbound type that can optionally anchor to a Check (RFC 0001 §4.2)
}
```

This is additive metadata over facts that already exist in the running system (a dispatcher is registered, or it isn't), not a new authorization mechanism — nothing today checks these flags to *allow or deny* an operation; §4.4 covers where they get consumed.

Mapped against the current registry (from investigation, not guesswork):

| `IntegrationType` | Direction | Capabilities |
|---|---|---|
| `GoogleCloud` | Outbound | `RequiredByCheckType` (`GcpCloudRunJobCheckExecutor`) |
| `Jira` | Outbound | *(none yet — config exists in the admin form, but no backend consumer implemented; manifest reflects that honestly instead of inventing a capability)* |
| `Email`, `Telegram`, `Twilio`, `MSTeams`, `Opsgenie`, `Pushover`, `Ntfy` | Outbound | `SendsPersonalNotification` |
| `PagerDuty` | Outbound | *(none — declared as a live type but no dispatcher registered today; the manifest surfaces this gap explicitly instead of silently implying it works)* |
| `Webhook`, `Slack`, `GoogleChat`, `Discord` | Outbound | *(none — `[Obsolete]`, manifest omits capabilities entirely rather than describing dead code)* |
| `GcpCloudMonitoringWebhook` (RFC 0001) | Inbound | `CreatesAlerts`, `SupportsEscalationPolicy`, `SupportsCheckCorrelation` |

### 4.3 `ConfigType` — a real class per type, not a hand-written field list

Rather than hand-writing a `ConfigFieldSchema[]` per type (which risks drifting from what the code actually deserializes — exactly the drift risk in §8), each `IntegrationType` gets a **real C# class** describing its `ConfigJson` shape, annotated with standard Data Annotations plus one new attribute for secrecy:

```csharp
public sealed class JiraConfig
{
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, SecretField]
    public string ApiToken { get; set; } = string.Empty;

    [Required]
    public string ProjectKey { get; set; } = string.Empty;

    [Required]
    public string IssueType { get; set; } = string.Empty;
}
```

`[SecretField]` (new attribute) is the direct replacement for `IntegrationExtensions.SecretKeysByType` (`IntegrationExtensions.cs:16-27`) — instead of a separate dictionary keyed by `IntegrationType` that has to be kept in sync with the real config shape by hand, the secret flag lives *on the property that is the secret*, in the same class that defines the shape. There's only one place to update when a field is added or changed, and it's impossible for the schema and the actual deserialization target to disagree — they're the same type.

The manifest's `ConfigType` (§4.1) is `typeof(JiraConfig)`. Three consumers read it via reflection, not three hand-maintained lists:

1. **Schema for the admin UI** — `GET /api/v1/integrations/types` (§4.4) reflects over `ConfigType`'s public properties, reading Data Annotations (`[Required]`, `[Url]`, `[EmailAddress]`, ...) plus `[SecretField]` to build the `ConfigFieldSchema[]`-shaped JSON the frontend consumes. This is generated, not authored — the previous `ConfigFieldSchema` record still exists as the *wire format*, it's just derived by reflection instead of written by hand per type.
2. **Deserializing `Integration.ConfigJson`** — `JsonSerializer.Deserialize(integration.ConfigJson, manifest.ConfigType)`, resolved from `integration.Type` → its manifest → `ConfigType`. Callers that need a specific integration's config (e.g. a future GCP webhook handler reading its auth token) get a real typed object instead of manually parsing a JSON blob, and `[Required]`/`[Url]`/etc. can be validated via `Validator.TryValidateObject` at the point `ConfigJson` is saved, catching a malformed config before it's persisted rather than failing later at dispatch/receive time.
3. **Masking secrets** — `IntegrationExtensions.MaskSecrets` rewritten to reflect over `ConfigType`'s properties, masking any marked `[SecretField]`, instead of looking up `SecretKeysByType[integration.Type]`.

`ConfigFieldType` (`String` | `Secret` | `Url` | `Enum`, etc., in the wire format) is derived from the Data Annotation present on each property (`[Url]` → `Url`, `[SecretField]` → `Secret`, otherwise `String`), not hand-assigned.

### 4.4 Where this gets read

- `IntegrationExtensions.MaskSecrets` — rewritten to reflect over `manifest.ConfigType`'s `[SecretField]`-marked properties instead of looking up `SecretKeysByType`.
- `CheckTypesController` (`CheckTypesController.cs:26-27`) — `RequiredByCheckType` on a manifest becomes an alternate source for what `RequiresIntegrationAttribute` provides today; both can coexist during migration (§6), one is not required to remove the other in this RFC.
- A new `GET /api/v1/integrations/types` (or extending an existing integration-types listing endpoint, to be confirmed during implementation) exposes each type's manifest to `apps/admin`, including the reflected `ConfigFieldSchema[]` derived from `ConfigType` (§4.3) — this is what unlocks (but does not itself build) schema-driven forms.
- Any code needing an `Integration`'s parsed config (e.g. a webhook handler reading its auth token) deserializes `ConfigJson` into `manifest.ConfigType` via reflection, instead of manually indexing into a parsed `JsonDocument`.
- `AlertLifecycleService`/webhook endpoints (RFC 0001) can assert `CreatesAlerts`/`SupportsEscalationPolicy` at startup as a sanity check that a new inbound type was wired up completely, rather than a runtime `NullReferenceException` surfacing the gap later.

### 4.5 What does NOT change

- `INotificationDispatcher`, `ICheckExecutor`, `RequiresIntegrationAttribute`, `IntegrationCategoryAttribute`, `PersonalChannelAttribute` — all untouched. The manifest is additive metadata read by new call sites; it doesn't replace or refactor these interfaces in this RFC.
- `Integration` entity schema — no new columns. The manifest is static, code-level metadata per `IntegrationType`, not per-row data.
- `apps/admin`'s existing per-type config components — untouched; they keep working exactly as today. Whether/when to replace them with a schema-driven renderer is separate follow-up work (§2).

## 5. Data / schema scope

- New: `IntegrationManifestAttribute`/`IntegrationManifest` record, `IntegrationDirection` enum, `IntegrationCapability` flags enum, `SecretFieldAttribute`, `ConfigFieldSchema`/`ConfigFieldType` (now a reflected wire format, not hand-authored — §4.3).
- New: one `TConfig` class per `IntegrationType` that has a `ConfigJson` shape today (e.g. `JiraConfig`, `PagerDutyConfig`, ...), annotated with Data Annotations + `[SecretField]`, matching what's currently only encoded in `apps/admin`'s per-type components.
- New: a manifest registration per existing `IntegrationType` (§4.2 table) — a static lookup (dictionary or attribute-driven, to be decided during implementation) analogous to how `IntegrationCategoryAttribute` is already declared per enum value today.
- No changes to `Integration`, `Check`, `Alert`, or any other entity's persisted schema — `ConfigJson` stays a `string` column; the `TConfig` class is only how it's serialized/deserialized in code, not a schema change to the table.

## 6. Phased plan

1. **Define the manifest types, `SecretFieldAttribute`, and one `TConfig` class per existing type** for all 14 `IntegrationType` values per §4.2's table — purely additive, no behavior change, verifiable by a unit test asserting every enum value (except explicitly-excluded obsolete ones) has a manifest entry and its `TConfig` round-trips through `Validator.TryValidateObject` against the actual `ConfigJson` currently produced by `apps/admin`'s existing forms.
2. **Wire `MaskSecrets` to reflect over `[SecretField]`** instead of `SecretKeysByType`, with a test asserting identical masking output before/after for all current types.
3. **Land RFC 0001's `GcpCloudMonitoringWebhook`** with its manifest entry and `GcpCloudMonitoringWebhookConfig` class from day one (`Inbound`, `CreatesAlerts`/`SupportsEscalationPolicy`/`SupportsCheckCorrelation`) — the first type designed manifest-first rather than retrofitted.
4. **Expose manifests via API** to `apps/admin`, including the reflected `ConfigFieldSchema[]` — read-only, informational at first (e.g. a badge showing "inbound" vs "outbound" on the integrations list).
5. **(Out of scope) Schema-driven admin forms** — replace the 13 hardcoded `*Config.tsx` components with one renderer driven by the reflected `ConfigFieldSchema[]`, if/when that refactor is worth doing.
6. **(Out of scope) Package-based extensibility** — see §2.

## 7. Alternatives considered

- **Keep `RequiresIntegrationAttribute`/`IntegrationCategoryAttribute` as separate, uncoordinated attributes and just add a fourth for direction.** Rejected: it perpetuates the actual problem (facts about a type scattered across independent attributes with no single read path), it just adds a fourth scattered fact instead of consolidating the existing three.
- **Store the manifest as data in the database** (a `IntegrationTypeManifest` table) instead of static code metadata. Rejected for this phase: `IntegrationType` itself is a compiled enum, not dynamic data — a DB-backed manifest for a compile-time-fixed set of types adds migration overhead for no benefit until package-based extensibility (§2) actually requires types to be introspected without a rebuild.

## 8. Risks

- **`Capabilities` drift** (unlike `ConfigType`, which structurally can't drift from itself — §4.3 — `Capabilities` is still a hand-set flag, independent of whether a dispatcher is actually registered): nothing enforces that a manifest's `Capabilities` stays true as dispatchers are added/removed over time (e.g. if `PagerDuty` later gets a real dispatcher, someone has to remember to flip its manifest too). Worth a unit test asserting `SendsPersonalNotification` in the manifest matches actual `INotificationDispatcher` registrations, to catch drift at build time rather than relying on manual discipline.
- **Encoding "no capability yet" without it reading as broken**: `PagerDuty`/`Jira` having empty `Capabilities` is a true, current fact (no dispatcher, no consumer), not a bug — but it needs to be presented in the admin UI (§4.4) as "not yet wired up," not as an error, so operators don't think Piro is malfunctioning when they configure one of these types today.
- **Reflection cost**: building `ConfigFieldSchema[]` and masking secrets via reflection on every request is unnecessary overhead — the reflected metadata per `TConfig` type never changes at runtime, so it should be computed once (e.g. cached in a static `ConcurrentDictionary<Type, ConfigFieldSchema[]>` keyed by `ConfigType`) rather than re-reflected per HTTP request.
