---
rfc: 13
title: "Heartbeat check type"
status: implemented
created: 2026-07-18
depends-on: ["0011"]
---

# RFC 0013 — Heartbeat check type

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-18

## 1. Problem

Every check Piro runs today is **outbound**: a worker actively probes a target and reports what it saw. `ICheckExecutor.ExecuteAsync(Check check, ...)` ([`ICheckExecutor.cs:13`](../../src/Piro.Application/Interfaces/ICheckExecutor.cs)) is a pull — HTTP fetches a URL, TCP opens a socket, gRPC calls the health service. This model can't observe a target that Piro cannot reach: a cron job, a batch worker, a backup script, a device behind NAT, a private k8s pod with no Ingress. Their liveness signal only exists at the target; there is nothing for a worker to connect to.

The platform already anticipates this. `CheckType.Heartbeat` exists in the enum ([`CheckType.cs:45`](../../src/Piro.Domain/Enums/CheckType.cs)) but is deliberately inert — the only value with no `[CheckTypeManifest]` attribute, no config record, and no executor. `IncidentTitleFactory` already maps it to `"Heartbeat missing"` ([`IncidentTitleFactory.cs:15`](../../src/Piro.Application/Services/IncidentTitleFactory.cs)), and GitHub issue [#1](https://github.com/Heva-Co/piro/issues/1) tracks its implementation. Because it has no executor, a Heartbeat check scheduled today would fall into the no-executor branch of `LocalCheckJobDispatcher` ([`LocalCheckJobDispatcher.cs:28-49`](../../src/Piro.Infrastructure/Workers/LocalCheckJobDispatcher.cs)) and write a `MONITOR_OUTAGE` / `NO_DATA` datapoint on every cycle — silently useless.

A Heartbeat check inverts the direction: **the monitored target pings Piro** on a schedule, and Piro marks the check DOWN when a ping is overdue. This RFC defines how that inversion fits the existing check, scheduling, ingestion, alerting, and auth machinery without introducing a parallel pipeline.

> **RFC number.** Issue #1 and early discussion referred to this as "0012"; that number was already assigned to [0012 — Integration actions with dynamic UI](0012-integration-actions-with-dynamic-ui.md). Per the README's immutable-numbering rule, this RFC takes the next free integer, **0013**.

## 2. Non-goals

- **A generic inbound webhook / push-metrics API.** Heartbeat is a single binary liveness signal ("I'm alive"), not an arbitrary metric ingest. RFC [0001](0001-third-party-alert-ingestion.md) already covers third-party *alert* ingestion; this is narrower and check-shaped.
- **Payload-carrying pings.** The first version records only *that* a ping arrived and *when*. Attaching a status body, exit code, or duration to a ping is a natural extension but out of scope here.
- **A new authentication subsystem.** Heartbeat pings authenticate with a token that reuses the existing `ApiKey` table and `ApiKeyService` hashing — see §4.4. No new credential store, no per-endpoint secret column on `Check`.
- **Multi-region heartbeats.** Pings are recorded in one region; a heartbeat check is forced single-region (§4.6). Fanning a single inbound signal across regions is meaningless.
- **Changing how outage *sensitivity* is configured.** "How many missed pings before alerting" is already expressible via `AlertConfig.FailureThreshold` ([`AlertConfig.cs:19`](../../src/Piro.Domain/Entities/AlertConfig.cs)); this RFC deliberately does not add a competing knob to the check config (§3, §4.2).

## 3. Design principle

**A heartbeat check is a normal check whose executor reads a stored timestamp instead of making a network call.** Everything downstream of "produce a `CheckExecutionResult`" — scheduling, dispatch, datapoint persistence, status recomputation, `CheckStatusChangedEvent`, alert-threshold evaluation, incident creation — is reused unmodified. The only genuinely new surfaces are (a) an inbound endpoint that records "last seen = now" and (b) an executor that turns "last seen vs. now" into UP/DOWN.

Two corollaries shape every choice below:

1. **The check reports raw state; the alert config decides significance.** Each scheduled tick emits UP or DOWN by comparing `now − lastSeen` against a minimal jitter threshold. *Whether a run of DOWNs is worth alerting on* is `AlertConfig.FailureThreshold`, exactly as for every other check type. The heartbeat config therefore carries only a jitter allowance, never an outage-tolerance count.
2. **Cron is the source of truth for cadence.** The check's `Cron` ([`Check.cs:23`](../../src/Piro.Domain/Entities/Check.cs)) *is* the expected ping interval — the user sets their pinger and the check to the same schedule. The expected interval is derived from the cron via the existing `ICronIntervalCalculator.SmallestInterval` ([`ICronIntervalCalculator.cs:16`](../../src/Piro.Application/Interfaces/ICronIntervalCalculator.cs)), not stored as a duplicate field.

## 4. Design

```
  ┌─────────────────────────────────────────────────────────────────────┐
  │  INBOUND (event-driven) — the target pings Piro                       │
  │                                                                       │
  │  target's cron ──GET/POST /api/v1/services/{s}/checks/{c}/heartbeat   │
  │                     ?token=hb_...                                      │
  │                        │                                              │
  │                 HeartbeatController  [no auth scheme; token is auth]  │
  │                        │ validate hb_ token (scope=Heartbeat,         │
  │                        │ CheckId matches) via ApiKeyService           │
  │                        ▼                                              │
  │            record ping CheckDataPoint (Status=UP, DataType=REALTIME,  │
  │            Timestamp=now)  ──► IngestAsync  ──► CurrentStatus=UP,     │
  │                                               fire event, eval alerts │
  └─────────────────────────────────────────────────────────────────────┘

  ┌─────────────────────────────────────────────────────────────────────┐
  │  SCHEDULED (overdue sweep) — Piro evaluates staleness                 │
  │                                                                       │
  │  Quartz (check.Cron) ─► CheckExecutionJob ─► CheckRunnerService       │
  │      ─► RoutingCheckJobDispatcher ─► LocalCheckJobDispatcher          │
  │              │  _executors[CheckType.Heartbeat]                       │
  │              ▼                                                        │
  │        HeartbeatCheckExecutor.ExecuteAsync(check)                     │
  │              │  lastSeen = repo.GetLatestByCheckIdAsync(check.Id)     │
  │              │  expected = SmallestInterval(check.Cron)               │
  │              │  grace    = config.GracePeriodSeconds                  │
  │              ▼                                                        │
  │        lastSeen==null                → NO_DATA                        │
  │        now-lastSeen ≤ expected+grace → UP                            │
  │        now-lastSeen >  expected+grace → DOWN                          │
  │              │                                                        │
  │              ▼  CheckExecutionResult                                 │
  │        IngestAsync ─► CurrentStatus, event, alert thresholds         │
  │                       (FailureThreshold decides DOWN→incident)        │
  └─────────────────────────────────────────────────────────────────────┘
```

Two independent write paths, both landing in the same `CheckDataPoint` table and both flowing through the same `ICheckResultIngester.IngestAsync`. Neither path adds new status/alert plumbing.

### 4.1 `CheckType.Heartbeat` manifest

`Heartbeat` gains the `[CheckTypeManifest]` attribute it currently lacks, mirroring the `GRPC` declaration ([`CheckType.cs:47-51`](../../src/Piro.Domain/Enums/CheckType.cs)):

```csharp
[CheckTypeManifest("Heartbeat",
    "External systems ping an inbound URL on a schedule; a missed ping marks the check DOWN.",
    typeof(HeartbeatCheckConfig), 60,
    [AlertFor.Status])]
Heartbeat,
```

The manifest constructor is `(displayName, description, configType, minIntervalSeconds, allowedAlertFors)` ([`CheckTypeManifestAttribute.cs:14-20`](../../src/Piro.Domain/Attributes/CheckTypeManifestAttribute.cs)). `AllowedAlertFors` is `[AlertFor.Status]` only — latency, cert expiry, and name-server metrics are meaningless for a heartbeat. `minIntervalSeconds = 60` matches every other type.

Adding the manifest is what makes `GetManifest()` return non-null for Heartbeat ([`CheckTypeExtensions.cs:15-18`](../../src/Piro.Domain/Extensions/CheckTypeExtensions.cs)), which in turn makes it appear in `GET /api/v1/checks/types` (§4.5). No change to `AllowedAlertFors` / `MinInterval` / `EnsureIntervalAllowed` — they read the manifest reflectively and start working for Heartbeat automatically.

**Stale-comment cleanup.** Several doc comments describe "Heartbeat / GRPC" as the unmanifested types — the enum-level comment ([`CheckType.cs:9-10`](../../src/Piro.Domain/Enums/CheckType.cs)) and the remarks in `CheckTypeExtensions` ([`:12`](../../src/Piro.Domain/Extensions/CheckTypeExtensions.cs)). GRPC has since been manifested and given an executor; after this RFC, *no* declared type is unmanifested. These comments are corrected as part of the change.

### 4.2 `HeartbeatCheckConfig`

A new config record under [`src/Piro.Domain/Checks/Config/`](../../src/Piro.Domain/Checks/Config/), following the `[ConfigField]` convention of `GrpcCheckConfig` ([`GrpcCheckConfig.cs`](../../src/Piro.Domain/Checks/Config/GrpcCheckConfig.cs)):

```csharp
/// <summary>Configuration for a push-based Heartbeat check.</summary>
public record HeartbeatCheckConfig
{
    [ConfigField("Grace period (seconds)",
        HelpText = "Slack beyond the expected ping interval (derived from the schedule) to absorb "
                 + "network jitter and clock drift before a tick reports DOWN.")]
    public int GracePeriodSeconds { get; init; } = 30;
}
```

**One field, on purpose.** The config holds *only* a jitter allowance. It deliberately does **not** hold an "expected interval" (derived from `Cron` — §4.3) nor a "missed pings tolerated" count. That second knob already exists, correctly, as `AlertConfig.FailureThreshold` ("Consecutive failures before the alert is triggered", [`AlertConfig.cs:18-19`](../../src/Piro.Domain/Entities/AlertConfig.cs)). Duplicating it in the check config would create two overlapping sensitivity controls with undefined interaction. The split is clean:

- **`GracePeriodSeconds`** (check config) = tolerance for *clock noise* — the ping and the sweep are independent clocks, so a live target's `now − lastSeen` naturally oscillates up to one full interval plus jitter. Grace absorbs that so a healthy target never flaps DOWN.
- **`FailureThreshold`** (alert config) = tolerance for *real missed pings* — how many consecutive overdue ticks constitute an outage worth paging on.

Worked example, cron `* * * * *` (every minute), `GracePeriodSeconds = 30`, `FailureThreshold = 2`:

| t (s) | event | `now − lastSeen` | tick result |
|---|---|---|---|
| 0 | ping | — | (UP on ingest) |
| 60 | sweep | 60 | UP (≤ 90) |
| 59 | ping | — | (UP) |
| 120 | sweep | 61 | UP (≤ 90) |
| — | *pings stop* | | |
| next sweep | sweep | 90–150 | first DOWN |
| following sweep | sweep | 150–210 | second DOWN → alert fires |

A live target with normal jitter never crosses `expected + grace = 90s`; a genuinely dead target produces consecutive DOWNs, and the *alert* waits for `FailureThreshold` of them.

### 4.3 Expected interval from cron

The executor derives the expected ping interval from the check's own schedule using the existing singleton `ICronIntervalCalculator` ([registered at `InfrastructureServiceExtensions.cs:167`](../../src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs)):

```csharp
var expected = cronIntervalCalculator.SmallestInterval(check.Cron) ?? TimeSpan.FromSeconds(60);
```

`SmallestInterval` samples consecutive fire times and returns the tightest gap, or `null` for a malformed / un-sampleable cron ([`QuartzCronIntervalCalculator.cs:16-46`](../../src/Piro.Infrastructure/Jobs/QuartzCronIntervalCalculator.cs)). The `?? 60s` fallback keeps the executor safe if the cron can't be sampled (which shouldn't happen for a persisted check, since scheduling already parsed it). No new interval field on `Check` or in the config — the schedule the user already set *is* the expected cadence.

### 4.4 Token: `ApiKey` extended with a scope

The inbound endpoint is called by machines (the target's cron/CI/systemd), not by a logged-in user, so it needs a credential embedded in a URL or header. Rather than add a bespoke `HeartbeatTokenHash` column to `Check` and re-implement generation/hashing/masking/revocation, this reuses the existing `ApiKey` infrastructure — the `ApiKeys` DbSet ([`PiroDbContext.cs:37`](../../src/Piro.Infrastructure/Persistence/PiroDbContext.cs)), the SHA-256-hash-at-rest scheme, `MaskedKey`, `LastUsedAt`, and `RevokeAsync` ([`ApiKeyService.cs`](../../src/Piro.Infrastructure/Auth/ApiKeyService.cs)).

**The constraint that forces a change.** A plain `ApiKey` authenticates *as its owning user with all their roles*: `ApiKeyAuthenticationHandler` resolves the user and copies every role into the claims so `[Authorize(Roles=...)]` works unchanged ([`ApiKeyAuthenticationHandler.cs:47-55`](../../src/Piro.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs)). A heartbeat token travels in cron logs, CI configs, and copy-pasted URLs — treating it as a full-privilege user key would mean a leaked ping URL is total account compromise. Relaxing that (e.g. making the handler emit fewer roles for *some* keys without a discriminator) would silently weaken every API key. The right move is the smallest structural addition that lets a key say "I am not a user credential."

Two new columns on `ApiKey`:

```csharp
public ApiKeyScope Scope { get; set; } = ApiKeyScope.Full;   // Full | Heartbeat
public int? CheckId { get; set; }                            // set iff Scope == Heartbeat
```

with a new enum:

```csharp
public enum ApiKeyScope { Full, Heartbeat }
```

- **`Full`** (default) — today's behavior, unchanged. Existing rows migrate to `Full` with `CheckId = null`.
- **`Heartbeat`** — bound to exactly one check (`CheckId`), authorizes *only* pinging that check, carries **no** role claims and **no** owning-user identity into the pipeline.

Generation reuses `ApiKeyService` with a distinct prefix so the two kinds are visually unmistakable: `hb_{64 hex}` instead of `pk_{64 hex}` ([`ApiKeyService.cs:24`](../../src/Piro.Infrastructure/Auth/ApiKeyService.cs)). A heartbeat key is created together with the check (and re-issuable via a "rotate token" action, which revokes the old row and issues a new one). The raw token is shown once, exactly like `pk_` keys.

**Auth handler split.** `ApiKeyAuthenticationHandler` continues to serve `Full` keys unchanged. Heartbeat validation does **not** go through that handler at all — the inbound endpoint validates the token itself (§4.4 below mirrors the webhook pattern), because a heartbeat key must never produce a `ClaimsPrincipal` that any `[Authorize]` endpoint would accept. Concretely, `ApiKeyService` gains a scoped lookup, e.g.:

```csharp
Task<ApiKey?> ValidateHeartbeatAsync(string rawKey, int checkId, CancellationToken ct = default);
```

which matches on `HashedKey`, `Status == Active`, `Scope == Heartbeat`, **and** `CheckId == checkId`, updates `LastUsedAt`, and returns the row (or null). It never touches `UserManager`. The existing `ValidateAsync(rawKey)` used by the auth handler is additionally constrained to `Scope == Full` so a heartbeat key can never authenticate a normal API request even if someone puts it in an `X-Api-Key` header.

### 4.5 Inbound ping endpoint

A new `HeartbeatController`, unauthenticated at the pipeline level exactly like `WebhooksController` (which carries no `[Authorize]` and self-validates a query token — [`WebhooksController.cs`](../../src/Piro.Api/Controllers/WebhooksController.cs)). The route nests under the existing service/check slug convention already used by `AlertConfigsController` (`api/v1/services/{serviceSlug}/checks/{checkSlug}/alert-configs`, [`AlertConfigsController.cs:11`](../../src/Piro.Api/Controllers/AlertConfigsController.cs)):

```
GET  /api/v1/services/{serviceSlug}/checks/{checkSlug}/heartbeat?token=hb_...
POST /api/v1/services/{serviceSlug}/checks/{checkSlug}/heartbeat
       token via ?token=hb_...  or  X-Heartbeat-Token header
```

**Both verbs, one handler.** GET makes the endpoint trivially callable from `curl`, `wget`, a browser, or a hosted cron pinger with no body; POST suits callers that prefer it. The handler:

1. Resolves the check by service slug + check slug using the established pattern in `CheckAppService.GetBySlugAsync` ([`CheckAppService.cs:45-52`](../../src/Piro.Application/Services/CheckAppService.cs)). Unknown → `404`.
2. Rejects checks whose `Type != Heartbeat` → `404` (the ping route is meaningless for other types; 404 rather than 400 avoids confirming the check's kind to an unauthenticated caller).
3. Validates the token via `ApiKeyService.ValidateHeartbeatAsync(token, check.Id)`, using the same constant-time comparison posture as the GCP webhook (`CryptographicOperations.FixedTimeEquals`, [`GcpWebhookIngestionService.cs:134`](../../src/Piro.Application/Services/GcpWebhookIngestionService.cs) — here the equality is on the SHA-256 hashes). Missing / wrong / wrong-check / revoked → `401`.
4. Records a ping datapoint (`Status = UP`, `DataType = REALTIME`, `Timestamp = now`, minute-aligned like every other datapoint) and runs it through `ICheckResultIngester.IngestAsync` ([`ICheckResultIngester.cs:12`](../../src/Piro.Application/Interfaces/ICheckResultIngester.cs)) so `CurrentStatus` flips to UP immediately, a `CheckStatusChangedEvent` fires, and recovery-side alert thresholds evaluate — the target is back the instant its ping lands, without waiting for the next sweep.
5. Returns `204 No Content` on success.

Only a wrong/missing token (`401`) or an unknown check (`404`) produces a non-2xx — matching the webhook contract's "only auth/identity failures are non-2xx" posture.

### 4.6 `HeartbeatCheckExecutor`

A normal `ICheckExecutor` ([`ICheckExecutor.cs`](../../src/Piro.Application/Interfaces/ICheckExecutor.cs)) with `CheckType => CheckType.Heartbeat`, so `LocalCheckJobDispatcher`'s `CheckType→executor` dictionary ([`LocalCheckJobDispatcher.cs:23-24`](../../src/Piro.Infrastructure/Workers/LocalCheckJobDispatcher.cs)) picks it up with zero dispatcher changes. It makes no network call — it reads the last ping:

```csharp
public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
{
    var config  = JsonSerializer.Deserialize<HeartbeatCheckConfig>(check.TypeDataJson) ?? new();
    var expected = _cron.SmallestInterval(check.Cron) ?? TimeSpan.FromSeconds(60);
    var grace    = TimeSpan.FromSeconds(config.GracePeriodSeconds);

    var latest = await _dataPoints.GetLatestByCheckIdAsync(check.Id, ct: ct);   // new repo method, §5
    if (latest is null)
        return new CheckExecutionResult(ServiceStatus.NO_DATA, null, "No heartbeat received yet.");

    var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(latest.Timestamp);
    return age <= expected + grace
        ? new CheckExecutionResult(ServiceStatus.UP, null, null)
        : new CheckExecutionResult(ServiceStatus.DOWN, null,
            $"No heartbeat in {(int)age.TotalSeconds}s (expected within {(int)(expected + grace).TotalSeconds}s).");
}
```

- **`NO_DATA` before the first ping ever.** A freshly created heartbeat check whose target hasn't been wired up yet is *not* an outage — it's "not configured on the sender side." Returning `NO_DATA` (rather than DOWN) keeps it out of alerting until a real ping establishes a baseline, and matches how the platform already treats absent data.
- **UP/DOWN thereafter** by the age comparison above. The *alert* decision on a run of DOWNs is `FailureThreshold`, untouched here.

Registration: add `services.AddScoped<ICheckExecutor, HeartbeatCheckExecutor>();` to the **API** block ([`InfrastructureServiceExtensions.cs:119-125`](../../src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs)). It is **not** added to the worker-only block ([`:276-281`](../../src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs)) — remote workers can't see the region where pings were ingested, so a heartbeat must be evaluated in-process where its datapoints live. This is the same reason `GcpCloudRunJobCheckExecutor` is API-only.

**Forced single-region.** Because routing keys solely on `check.IsMultiRegion` ([`RoutingCheckJobDispatcher.cs:23-24`](../../src/Piro.Infrastructure/Workers/RoutingCheckJobDispatcher.cs)), a heartbeat check must keep `IsMultiRegion = false` so the sweep runs on `LocalCheckJobDispatcher`. This is enforced at check-create/update validation (a heartbeat check with `IsMultiRegion = true` is rejected), and the admin form hides the multi-region toggle for Heartbeat (§4.7).

### 4.7 Admin UI (`apps/admin`)

Heartbeat is configured and operated from the admin panel; nothing on the public status page (`apps/web`) changes.

**Config form — auto-generated.** Because `Heartbeat` now has a manifest, `GET /api/v1/checks/types` includes it: `CheckTypesController` builds the runnable set from registered `ICheckExecutor`s and serializes each manifested type via `ToMetaDto(hasExecutor: ...)` ([`CheckTypesController.cs:25-31`](../../src/Piro.Api/Controllers/CheckTypesController.cs)). With the executor registered (§4.6), Heartbeat is returned with **`HasExecutor = true`** and a `ConfigSchema` reflected from `HeartbeatCheckConfig` by `ConfigSchemaBuilder` ([`ConfigSchemaBuilder.cs`](../../src/Piro.Application/Extensions/ConfigSchemaBuilder.cs)). The existing admin check-config form renders the single **Grace period (seconds)** number input (label + help text from `[ConfigField]`) with no bespoke component — the same mechanism that renders the gRPC form.

**Multi-region toggle hidden.** The check-create/edit form suppresses the "multi-region" control when the selected type is Heartbeat (§4.6), so the user can't build an invalid configuration.

**Ping URL + token — the one bespoke surface.** The check *detail* page gains a "Heartbeat" panel that shows:
- the full ping URL with the token substituted in, `.../services/{slug}/checks/{checkSlug}/heartbeat?token=hb_…`, with a **Copy** button and a short "call this from your cron / CI on the same schedule as the check" hint;
- a **Rotate token** action (revokes the current `hb_` `ApiKey` row and issues a new one, showing the new raw token once);
- the masked token and `LastUsedAt` ("last ping received") read back from the `ApiKey` row, so the user can confirm pings are arriving.

The raw token is returned exactly once — on check creation and on rotate — mirroring the existing `pk_` key UX. After that only the `MaskedKey` is shown.

**Type generation.** After the DTO/enum changes land, regenerate the admin API types (`pnpm run generate:api-types` in `apps/admin`) so `HeartbeatCheckConfig`, the new `ApiKeyScope`, and any create/rotate response shape are available to the frontend without hand-written interfaces.

### 4.8 What does NOT change

The point of this design is that the inversion is confined to two new surfaces; everything else is reused verbatim:

- **`ICheckExecutor` contract** — the heartbeat executor implements it unchanged; the "read a timestamp" body is an implementation detail.
- **Scheduler → job → runner → dispatcher chain** — `CheckSchedulerService`, `CheckExecutionJob`, `CheckRunnerService`, `RoutingCheckJobDispatcher`, `LocalCheckJobDispatcher` are untouched. Heartbeat rides the exact Quartz-cron path every other check uses.
- **`ICheckResultIngester`** — both the inbound ping and the sweep call `IngestAsync` ([`ICheckResultIngester.cs:12`](../../src/Piro.Application/Interfaces/ICheckResultIngester.cs)); status recomputation, `CheckStatusChangedEvent`, and alert-threshold evaluation are entirely reused. No new ingest path.
- **`AlertConfig` / alert lifecycle** — no new fields, no new `AlertFor`. Outage sensitivity is `FailureThreshold`, which already means "consecutive failures before alert." Incident titling already handles Heartbeat ([`IncidentTitleFactory.cs:15`](../../src/Piro.Application/Services/IncidentTitleFactory.cs)).
- **`ApiKeyAuthenticationHandler` for `Full` keys** — unchanged; heartbeat keys never flow through it (§4.4).
- **`CheckDataPoint` schema** — a ping is an ordinary datapoint (`REALTIME` / `UP`); no new `DataPointType`. The `MONITOR_OUTAGE` value ([`DataPointType.cs:22`](../../src/Piro.Domain/Enums/DataPointType.cs)) keeps its meaning; the no-executor branch that produced it for Heartbeat becomes dead for this type once the executor is registered.
- **Public status page (`apps/web`)** — a heartbeat check's UP/DOWN renders like any other check's status; no changes needed.

## 5. Data / schema scope

**Migrations (one, on `ApiKey`):**
- `ApiKey.Scope` — new column, enum stored as string (following the existing `Status` `HasConversion<string>()` convention in `ApiKeyConfiguration`, [`MiscConfiguration.cs:19`](../../src/Piro.Infrastructure/Persistence/Configurations/MiscConfiguration.cs)), default `Full`. Backfills existing rows to `Full`.
- `ApiKey.CheckId` — new nullable `int?` FK to `Check`, `OnDelete(Cascade)` (deleting a check removes its heartbeat key). Null for `Full` keys.
- Precedent: the ApiKey table has been migrated before for exactly this kind of additive change — `20260710164541_ApiKeyLastUsedAtAndStatusEnum` added `LastUsedAt` and converted `Status` to the enum-name representation.

**New enum:**
- `ApiKeyScope { Full, Heartbeat }` in `src/Piro.Domain/Enums/`.

**No changes to:**
- `CheckType` enum *values* — `Heartbeat` already exists ([`CheckType.cs:45`](../../src/Piro.Domain/Enums/CheckType.cs)); only its `[CheckTypeManifest]` attribute is added.
- `CheckDataPoint`, `DataPointType`, `AlertConfig`, `AlertFor`, `Check` — no schema changes. (`Check` stores heartbeat config in the existing `TypeDataJson`; the token lives on `ApiKey`, not on `Check`.)
- `ServiceStatus` — reuses `UP` / `DOWN` / `NO_DATA`.

**New code (no schema):**
- `HeartbeatCheckConfig` record (`src/Piro.Domain/Checks/Config/`).
- `HeartbeatCheckExecutor` (`src/Piro.Infrastructure/Checks/`), registered in the API DI block only.
- `HeartbeatController` (`src/Piro.Api/Controllers/`).
- `ICheckDataPointRepository.GetLatestByCheckIdAsync(int checkId, string? region = null, CancellationToken ct = default)` — a per-check "latest datapoint" accessor. Today the only "latest" method is `GetLatestByServiceIdAsync` ([`ICheckDataPointRepository.cs:34`](../../src/Piro.Application/Interfaces/ICheckDataPointRepository.cs)), which is service-scoped; the sweep needs per-check. (Achievable today via `GetByCheckIdAsync(checkId, limit: 1)` since it's newest-first, but a dedicated single-result method is clearer.)
- `ApiKeyService.ValidateHeartbeatAsync` + scope constraint on `ValidateAsync` (§4.4), and a heartbeat-key create/rotate method.

## 6. Phased plan

Each phase is independently shippable and testable.

1. **Manifest + config + executor (backend liveness detection).** Add the `[CheckTypeManifest]` to `Heartbeat`, the `HeartbeatCheckConfig` record, `GetLatestByCheckIdAsync`, and `HeartbeatCheckExecutor` (API-registered). At the end of this phase a heartbeat check evaluates staleness on schedule and reports NO_DATA/UP/DOWN — but there is not yet a way to *send* a ping (tested by seeding a `REALTIME` datapoint directly). Also correct the stale "Heartbeat / GRPC" doc comments.
2. **Scoped token + inbound endpoint.** Add `ApiKeyScope`, the `ApiKey.Scope` / `ApiKey.CheckId` migration, `ValidateHeartbeatAsync` + the `ValidateAsync` `Full` constraint, heartbeat-key creation on check-create, and `HeartbeatController` (GET + POST). At the end of this phase, external systems can ping and flip the check UP; single-region enforcement is validated here.
3. **Admin UI.** The auto-generated config form already works once Phase 1 lands; this phase adds the bespoke check-detail Heartbeat panel (ping URL + copy, rotate token, last-ping-received), hides the multi-region toggle for Heartbeat, and regenerates `api-types.ts`.

## 7. Alternatives considered

- **A separate `HeartbeatTokenHash` column on `Check`** (instead of a scoped `ApiKey`). Rejected — it duplicates generation, SHA-256 hashing, masking, `LastUsedAt`, and revocation that `ApiKeyService` already implements, and scatters credential logic across two subsystems. The scoped-`ApiKey` addition is two columns and reuses all of it.
- **Reusing a plain `Full` `ApiKey` unchanged.** Rejected — a full key grants the owning user's entire role set ([`ApiKeyAuthenticationHandler.cs:47-55`](../../src/Piro.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs)); a heartbeat URL leaked from a cron log would be total account compromise. The `Scope` discriminator is the minimal structural fix that lets a key be non-privileged and check-bound.
- **A dedicated `IHostedService` sweep instead of an `ICheckExecutor`.** Rejected — it would be a parallel evaluation path with its own status/alert wiring, violating the design principle. Modeling the sweep as an executor means `IngestAsync`, `CheckStatusChangedEvent`, and `FailureThreshold` all work for free.
- **An `ExpectedIntervalSeconds` field in the config.** Rejected — it duplicates the check's `Cron`, inviting the two to disagree. Deriving `expected` from `SmallestInterval(check.Cron)` keeps a single source of truth for cadence.
- **A "missed pings tolerated" count in the config.** Rejected — this is exactly `AlertConfig.FailureThreshold` ([`AlertConfig.cs:18-19`](../../src/Piro.Domain/Entities/AlertConfig.cs)). A second knob would create two overlapping, independently-configurable sensitivity controls.
- **Marking `NO_DATA` as `DOWN` before the first ping.** Rejected — a check whose sender isn't wired up yet is unconfigured, not down; alerting on it at creation time would be noise. `NO_DATA` defers alerting until a real baseline exists.

## 8. Risks

- **The check has no heartbeat of its own.** If the *Piro* API (where the sweep runs) is itself down when a ping would have failed, or if the region ingesting pings goes dark, no sweep executes and the heartbeat can't self-report the gap — the same blast-radius limitation any single-region in-process check has. This is why heartbeat is forced single-region and why `MONITOR_OUTAGE` datapoints (from the scheduler when no worker is available) remain meaningful as a distinct "we couldn't observe" signal.
- **Clock skew between the target and Piro.** `now − lastSeen` compares the target's send time (as observed at ingest) against Piro's wall clock. Large skew on the *target* side is invisible (we timestamp on receipt), which is the safe direction; skew only matters if the target's *cadence* drifts, which `GracePeriodSeconds` is there to absorb. Documented in the grace-period help text.
- **Token in the URL query string.** A `GET ?token=` is convenient but lands in access logs and browser history. Mitigations: the `X-Heartbeat-Token` header is offered for POST callers who care, the token is check-scoped and non-privileged (a leak pings one check, nothing more), and rotation is one click. Accepted as the cost of GET-with-no-body convenience.
- **Grace vs. cron mismatch.** If a user sets the check's cron finer than their actual ping cadence, `expected` will be smaller than reality and the check will flap. The detail-panel hint ("call this on the same schedule as the check") and the grace default mitigate it, but it's fundamentally a user-configuration concern — the same class of mistake as setting an HTTP check's interval shorter than the endpoint's response time.
