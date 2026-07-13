# RFC 0002 — Separate raw measurement from alert severity (Prometheus/Alertmanager-style)

Status: proposal
Author: Arael Espinosa (assisted draft)
Date: 2026-07-13

## 1. Problem

Piro's `Check` model mixes two responsibilities that should be separate: *measuring* a target and *judging how bad the result is*. Concretely:

1. **Dead severity fields on `Check`.** `Check.FailureThreshold`/`Check.RecoveryThreshold` (`src/Piro.Domain/Entities/Check.cs`) are read and written by `CheckAppService` but consulted by nothing. `Check.CurrentStatus` is assigned directly from the executor's raw result — `check.CurrentStatus = aggregatedResult.Status;` (`src/Piro.Application/Services/CheckResultIngesterService.cs:57`) — with no consecutive-failure counting at all. These two fields exist in the schema, are editable in the admin panel, and do nothing.
2. **Severity thresholds live in two places that don't agree.** `SslCheckExecutor.ClassifyExpiry` (`src/Piro.Infrastructure/Checks/SslCheckExecutor.cs`) already turns "days until expiry" into `DOWN`/`DEGRADED`/`UP` using `WarningDaysBeforeExpiry`/`CriticalDaysBeforeExpiry` baked into the check's own config. Separately, `AlertConfig.FailureThreshold`/`SuccessThreshold` (`src/Piro.Domain/Entities/AlertConfig.cs:19,22`) — the only threshold that's actually wired up, via `AlertEvaluationService.CountConsecutive` (`src/Piro.Application/Services/AlertEvaluationService.cs`) — judges *again*, on top of a status the check already judged. A user configuring "when should this alert" has to reconcile two independent threshold surfaces that were never designed to agree with each other.
3. **`AlertFor` doesn't fit every `CheckType`.** The enum (`src/Piro.Domain/Enums/AlertFor.cs`) is `Status | Latency | Uptime`, shown identically for every check type in the admin panel (`ALERT_FOR_OPTIONS`, `apps/admin/src/features/checks/pages/CheckDetailPage.tsx:191-195`) regardless of whether that check type actually produces a comparable signal. `Uptime` is not implemented anywhere — `IsThresholdConditionMet` (`AlertEvaluationService.cs`) falls through to `_ => false` for it. An SSL check has no meaningful "Latency" signal to alert on; a `GCP_CloudRunJob` check's real signal (`MaxAgeHours`, already in `GcpCloudRunJobCheckData`) isn't exposed as an `AlertFor` option at all.
4. **One `AlertConfig` per `Check`, enforced by a unique index**, blocks the natural case of wanting two severities off one signal (e.g. "warn at 30 days until cert expiry, page at 7"). `IX_AlertConfigs_CheckId` is unique (`src/Piro.Infrastructure/Migrations/20260711042758_RestrictOneAlertConfigPerCheck.cs:30-34`), enforced again in application code by `AlertConfigAppService.CreateAsync` explicitly throwing if one already exists (`src/Piro.Application/Services/AlertConfigAppService.cs:40-42`) — while the repository (`GetByCheckIdAsync`, `AlertConfigRepository.cs:13-16`) and the client type (`AlertConfig[]` in `apps/admin/src/lib/api.ts:341-364`) are both list-shaped, and the check detail page collapses that list to a single record with `alertConfigs?.[0]` (`CheckDetailPage.tsx:206`) — three different layers each independently encoding "there's only one," with none of them actually modeling it as a hard 1:1.

## 2. Non-goals

- **Not rebuilding grouping/routing/silencing.** Alertmanager's actual job — deduplicating, grouping, routing severities to different receivers — is out of scope. This RFC only fixes *where a threshold is allowed to live*, not how alerts get delivered once fired (`AlertLifecycleService`, `EscalationCheckerService`, `INotificationDispatcher` are untouched — see §4.6).
- **Not changing `Service.PublicStatus` vs `Service.CurrentStatus` semantics.** Research surfaced that `apps/web` currently reads `currentStatus` (`ServiceStatusCard.tsx:29`) rather than the `PublicStatus` field `ServiceStatusService` already computes and persists specifically for public display (`ServiceStatusService.cs:130`). That's a real, separate bug — worth its own issue — but fixing it isn't part of this proposal and this RFC doesn't depend on it being fixed first.
- **Not touching `ServiceStatusService`'s worst-of aggregation algorithm** (`Worst(a,b)`, `ServiceStatusService.cs:141-142) or its maintenance-window/incident-impact/dependency inputs. This RFC changes *what feeds into* `Check.CurrentStatus`, not how `Service.CurrentStatus` is derived from checks.
- **Not proposing a generic metrics/time-series subsystem.** The new `CheckDataPoint.MetricValue` field (§4.2) is a single nullable number for "the one number this check's severity should be judged on," not a multi-metric schema. If a check type ever needs more than one comparable number, that's a follow-up RFC.

## 3. Design principle

**A `Check` measures; an `AlertConfig` judges.** A check execution can only report two things about itself: it *ran and produced a result* (a raw number or a pass/fail from the target's perspective), or it *failed to run at all* (timeout, DNS didn't resolve, connection refused — the executor has nothing to measure). Whether a given result is fine, a warning, or critical is a judgment call that belongs entirely to `AlertConfig`, matching how Prometheus (exporters expose raw metrics; alerting rules in Alertmanager decide severity) and Datadog (a Monitor, not the underlying check, is the single source of severity) already split this. Every design choice below is either "moves a severity decision out of the check and into `AlertConfig`" or "gives `AlertConfig` the raw number it needs to make that decision."

## 4. Design

### 4.1 `AlertConfig` becomes N:1 with `Check`

Drop `IX_AlertConfigs_CheckId`'s uniqueness (`RestrictOneAlertConfigPerCheck` migration, reverse the `unique: true` at line 34) and delete the check-then-throw in `AlertConfigAppService.CreateAsync` (lines 40-42). This isn't a new capability bolted on — `AlertConfigRepository.GetByCheckIdAsync` already returns `IEnumerable<AlertConfig>` and the frontend's `AlertConfig[]` client type already assumes a list; only the DB constraint and one `if` block were holding the model to 1:1. The admin UI (`CheckDetailPage.tsx`) changes from "one form, edited in place" (lines 197-199, 206, 227-249) to a list of AlertConfig rows with add/edit/delete per row — same pattern the `NotificationPreferencesEditor` already uses for a user's N notification preferences, not a new UI paradigm.

This directly enables the SSL example from §1: two `AlertConfig` rows on one `Check` — `AlertFor: CertExpiry, AlertValue: "30", Severity: Warning` and `AlertFor: CertExpiry, AlertValue: "7", Severity: Critical`.

### 4.2 `CheckDataPoint` gains a raw metric slot

Add `public double? MetricValue { get; set; }` to `CheckDataPoint` (`src/Piro.Domain/Entities/CheckDataPoint.cs`), alongside the existing `Status`, `LatencyMs`, `ErrorMessage` fields (lines 13-19) — not replacing them. `LatencyMs` already is a raw metric in this same shape; `MetricValue` is the generalization for checks whose meaningful number isn't latency (days-until-expiry for SSL, hours-since-last-execution for `GCP_CloudRunJob`). Each executor decides whether it has a `MetricValue` to report; checks with no comparable single number (Ping, TCP) simply leave it null and keep alerting on `Status` only.

### 4.3 Executors stop judging severity — one subsection per `CheckType`

For each type: what it measures now, what judgment moves out, and what `AlertConfig`s make sense afterward.

**HTTP** (`HttpCheckData`/`HttpCheckExecutor`, `src/Piro.Infrastructure/Checks/HttpCheckExecutor.cs`)
- Keeps: response rules (`HttpResponseRule` — contains/regex/json_path/xml_path), expected status codes, `MetricValue = latency in ms` (mirrors existing `LatencyMs`).
- Removes: `DegradedLatencyMs`/`DownLatencyMs` from `HttpCheckData` — these currently let the check itself decide "over 2000ms = DEGRADED" independent of any `AlertConfig`.
- Executor now reports `UP` if the request completed and satisfied the configured response rules/status codes, `DOWN` only on timeout/connection failure (couldn't measure at all).
- Example `AlertConfig`s: `AlertFor: Latency, AlertValue: "2000", Severity: Warning` + `AlertFor: Latency, AlertValue: "5000", Severity: Critical`; `AlertFor: Status, AlertValue: "DOWN", Severity: Critical` for the executor-failure case.

**DNS** (`DnsCheckData`/`DnsCheckExecutor`)
- Keeps: resolved value comparison, `MetricValue = latency`.
- Removes: `DegradedLatencyMs`/`DownLatencyMs`, `DegradedAfter`/`DownAfter` from `DnsCheckData` — same pattern as HTTP, these are severity thresholds sitting on the check instead of on an `AlertConfig`.
- Executor reports `DOWN` only if resolution fails outright (no answer, NXDOMAIN when one was expected) or times out; a resolved-but-unexpected value becomes an `AlertFor: Status` (or a dedicated value-mismatch signal, evaluated during implementation) condition for `AlertConfig` to judge, not a DEGRADED the executor decides on its own.

**Ping** (`PingCheckData`/`PingCheckExecutor`) and **TCP** (`TcpCheckData`/`TcpCheckExecutor`)
- No severity fields exist on either today — nothing to remove. Both already report a binary reachable/unreachable from the executor's perspective, which is already "measurement, not judgment." `MetricValue = latency` is added for parity with HTTP/DNS (both executors already measure it internally; it's just not surfaced today), enabling an `AlertFor: Latency` config if a user wants to page on a slow-but-technically-up TCP/ping target.
- No change to what counts as `DOWN` — connection refused/timeout was always the only failure mode.

**SSL** (`SslCheckData`/`SslCheckExecutor`)
- Removes: `WarningDaysBeforeExpiry`/`CriticalDaysBeforeExpiry` from `SslCheckData`, and `ClassifyExpiry`'s three-way branch (`SslCheckExecutor.cs`) collapses to two cases: certificate expired or the connection/handshake failed outright → `DOWN` (couldn't establish a valid session at all); otherwise → `UP` with `MetricValue = days until expiry` (computed from `cert.NotAfter`, same value already computed today, just no longer classified inline).
- New `AlertFor.CertExpiry` (§4.4) reads that `MetricValue`. Example `AlertConfig`s: `AlertFor: CertExpiry, AlertValue: "30", Severity: Warning` + `AlertFor: CertExpiry, AlertValue: "7", Severity: Critical` — the exact two-tier case from §1 that the current 1:1 constraint blocks.

**GCP_CloudRunJob** (`GcpCloudRunJobCheckData`/`GcpCloudRunJobCheckExecutor`, `src/Piro.Infrastructure/Checks/GcpCloudRunJobCheckExecutor.cs`)
- `MaxAgeHours` (currently in `GcpCloudRunJobCheckData`, default 25) already lives on the check, but unlike SSL's day-thresholds it's arguably *configuration of what to measure* (how stale counts as "no recent execution"), not severity — there's no natural second reading of "hours since last run" the way SSL has two natural cutoffs. Recommendation: **keep `MaxAgeHours` on the check** (it's the equivalent of an HTTP check's URL — part of defining what's being measured, not a severity judgment), but report `MetricValue = hours since last successful execution` instead of pre-classifying it to `DOWN` inline, so `AlertConfig` can still choose a different/additional threshold if desired (e.g. warn earlier than the hard cutoff used to decide the job needs re-running).
- The `failedCount > 0 && succeededCount > 0` → `DEGRADED` branch (partial task failure within one execution) is a genuine measurement, not a severity call — it stays, reported as `Status` (there's no raw number to generalize a "some tasks failed" signal into).
- Executor reports `DOWN` only if the Cloud Run API call itself fails (network/auth error — couldn't measure).

### 4.4 `AlertFor` gets a `CheckType` whitelist, and a new value

```csharp
public enum AlertFor
{
    Status,
    Latency,
    CertExpiry,
}
```

`Uptime` is removed — it was never implemented (`IsThresholdConditionMet`'s `_ => false` fallthrough is its only trace). `CertExpiry` is added, comparing `AlertConfig.AlertValue` (parsed as an int, "days") against `CheckDataPoint.MetricValue` — same numeric-comparison shape `Latency` already has against `LatencyMs`, just pointed at the new generic field.

One map becomes the single source of truth for which `AlertFor` values are valid per `CheckType`, consumed by both sides instead of two independently-maintained lists:

```csharp
// src/Piro.Domain/... (exact location TBD during implementation — likely an extension
// method alongside CheckType, mirroring how PersonalNotificationChannelExtensions maps
// PersonalNotificationChannel -> IntegrationType today)
CheckType.HTTP           -> [Status, Latency]
CheckType.DNS            -> [Status, Latency]
CheckType.Ping           -> [Status, Latency]
CheckType.TCP            -> [Status, Latency]
CheckType.SSL            -> [Status, CertExpiry]
CheckType.GRPC           -> [Status, Latency]   // unimplemented CheckType, included for completeness
CheckType.GCP_CloudRunJob -> [Status]
CheckType.Heartbeat      -> [Status]            // per RFC 0001 §4.0, still unimplemented
```

- **Backend**: `AlertConfigAppService.CreateAsync`/`UpdateAsync` validate `request.AlertFor` against this map for the target check's `CheckType`, throwing `DomainValidationException` on mismatch — same exception type already used for the 1:1 violation being removed in §4.1.
- **Frontend**: `CheckDetailPage.tsx`'s static `ALERT_FOR_OPTIONS` (lines 191-195) becomes a lookup keyed by the check's `type`, filtering the picker instead of always showing all three (soon four) values.

### 4.5 `Check.CurrentStatus`'s only remaining job: "did the last execution succeed?"

`CheckResultIngesterService.IngestStatusOnlyAsync` (`CheckResultIngesterService.cs:51-68`) keeps assigning `Check.CurrentStatus` directly from the executor's result exactly as it does today (line 57) — **this does not change**. What changes is what that result *can be*: under §4.3, an executor only returns `UP` (ran, produced a value, whatever that value was) or `DOWN`/`FAILURE` (couldn't run at all). The DEGRADED state disappears from executor output entirely — a check is never "kind of working." `ServiceStatusService`'s worst-of aggregation (`ServiceStatusService.cs:141-142`) and its maintenance/incident-impact/dependency inputs are untouched; it still aggregates whatever `Check.CurrentStatus` values it's given, it just never receives an executor-assigned `DEGRADED` anymore.

`AlertConfig.Severity` (`Warning`/`Critical`) is what now carries the "how bad" judgment that used to live in the executor. `AlertLifecycleService.RecordOccurrenceAsync`'s existing `Severity == Critical ? DOWN : DEGRADED` mapping (`AlertLifecycleService.cs:45`) is unchanged — it already does exactly the severity→status translation this RFC wants, just previously fed by a check that had *also* already judged. This RFC removes the redundant judgment upstream, not the one at the alert layer.

```
Executor: measures → { ran: value } | { failed: timeout/DNS/refused/... }
              │                              │
              ▼                              ▼
    Check.CurrentStatus = UP          Check.CurrentStatus = DOWN
    CheckDataPoint.MetricValue = X
              │
              ▼
    AlertConfig(s) compare MetricValue/Status against their own AlertValue+Severity
              │
              ▼
    AlertConfig.Severity (Warning/Critical) → Alert.ImpactAtFireTime (DEGRADED/DOWN)
    (AlertLifecycleService.RecordOccurrenceAsync:45 — unchanged)
              │
              ▼
    Alert sits at Alert.ResolvedAt == null until resolved (unchanged)
    → EscalationCheckerService pages on-call (unchanged)
    → human decides whether to promote to Incident (unchanged, per RFC 0001's principle)
```

The visible "is this DEGRADED?" a user sees for a service now comes from whether a Warning-severity `Alert` is active for one of its checks, not from a check having decided DEGRADED on its own — consistent with how a Grafana panel reflects an alert rule's firing state rather than the exporter opining on its own health.

### 4.6 What does NOT change

- `ICheckExecutor` interface, `RoutingCheckJobDispatcher`/`LocalCheckJobDispatcher`/`RemoteCheckJobDispatcher` — untouched; executors still implement the same interface, they just return a narrower range of `ServiceStatus` values.
- `ServiceStatusService`'s worst-of aggregation, maintenance-window/incident-impact/dependency-status inputs (`ServiceStatusService.cs:29-59`) — unchanged; it keeps consuming `Check.CurrentStatus` exactly as today.
- `AlertLifecycleService.RecordOccurrenceAsync`/`ResolveActiveAlertAsync`, `AlertEvaluationService.CountConsecutive`'s consecutive-failure counting, `EscalationCheckerService`'s `Alert.ResolvedAt is null` query and per-Alert escalation state — all reused unmodified. This RFC changes what *feeds* `AlertConfig` evaluation (raw `MetricValue` instead of a pre-judged status, for the checks that need it), not the evaluation/escalation pipeline itself.
- `IncidentAppService`, `INotificationDispatcher`, manual alert→incident promotion — untouched, same as RFC 0001 preserves.
- `Service.PublicStatus`/`apps/web` consuming `currentStatus` instead — the discrepancy noted in §2 is real but independent of this proposal; not touched here.
- `Check.FailureThreshold`/`RecoveryThreshold` removal is included in §5, but note it's a pure deletion of dead code, not a behavior change — nothing reads them today, so nothing observes their absence tomorrow.

## 5. Data / schema scope

- `AlertFor` enum: remove `Uptime`, add `CertExpiry`.
- `AlertConfig` — no column changes. The 1:1 constraint removal is a migration reverting `IX_AlertConfigs_CheckId` from unique to non-unique (or dropping and recreating non-unique).
- `Check` — remove `FailureThreshold`, `RecoveryThreshold` (dead columns, confirmed unread anywhere in §1).
- `HttpCheckData`/`DnsCheckData` (both stored in `Check.TypeDataJson`, not separate columns) — remove `DegradedLatencyMs`/`DownLatencyMs` (both types) and `DegradedAfter`/`DownAfter` (DNS only) from the JSON shape. No migration needed for `TypeDataJson` itself (it's a JSON blob), but existing checks with these keys set will have them silently ignored post-deploy — see Risks (§8) for the backfill/communication concern.
- `SslCheckData` — remove `WarningDaysBeforeExpiry`/`CriticalDaysBeforeExpiry` from the JSON shape. Same "silently ignored" consideration as above.
- `CheckDataPoint` — add `MetricValue` (`double?`, nullable, no default) alongside existing `LatencyMs`. New migration, additive column, safe to deploy without backfill (existing rows get `NULL`).
- No changes to `Alert`, `AlertLifecycleService`'s schema-facing fields, `Incident`, `Service`, `EscalationPolicy`/`EscalationCheckerService`'s schema.

## 6. Phased plan

1. **Phase 1 — Unblock the data model**: migration removing the `AlertConfig`/`Check` unique constraint (§4.1), remove the app-layer check-then-throw, add `CheckDataPoint.MetricValue`, remove dead `Check.FailureThreshold`/`RecoveryThreshold` columns. No executor behavior changes yet — purely additive/dead-code-removal, independently shippable and low-risk.
2. **Phase 2 — `AlertFor` whitelist + `CertExpiry`**: add the enum value, the `CheckType → AlertFor[]` map (backend validation + frontend picker filtering per §4.4), update `AlertConfigAppService` validation. Still no executor changes — `CertExpiry` configs simply can't fire yet because no executor populates `MetricValue` for SSL until Phase 3.
3. **Phase 3 — Executor changes, one `CheckType` at a time**: SSL first (highest-value case per §1's motivating example — two-tier expiry warning), then HTTP/DNS (remove their dead-weight `Degraded*`/`Down*` fields), then Ping/TCP (add `MetricValue = latency` for parity), then `GCP_CloudRunJob` (report `MetricValue` alongside the existing `MaxAgeHours` comparison). Each is independently shippable and testable — the whitelist from Phase 2 means a `CheckType` with no executor changes yet simply can't have a `CertExpiry`/misapplied `AlertFor` created against it regardless of ordering.
4. **Phase 4 — Admin UX for N:1**: `CheckDetailPage.tsx`'s Alert Configuration section becomes a list (add/edit/delete rows) instead of the current single-form-editing-in-place UI (§4.1). Deferred to last because Phases 1-3 are usable via API/existing single-row UI in the interim (a check can still have exactly one `AlertConfig` even after the constraint is lifted — nothing forces a second one to exist until a user wants it).

## 7. Alternatives considered

- **Keep severity thresholds on the check, just document them better.** Rejected — it doesn't resolve the actual problem (two threshold surfaces that can disagree, e.g. an HTTP check's `DownLatencyMs=5000` and a separate `AlertConfig: Latency > 3000` firing independently with no relationship to each other), it only makes the confusion more visible.
- **Make `AlertConfig` derive its own thresholds automatically from the check's existing severity fields**, instead of removing the check-level fields. Rejected — this keeps two sources of truth in sync via inference instead of by construction, which is fragile (what happens when a user edits one but not the other?) and doesn't address dead fields like `FailureThreshold`/`RecoveryThreshold` that already silently do nothing.
- **A generic multi-metric `CheckDataPoint` schema** (e.g. a JSON blob of named metrics) instead of a single `MetricValue`. Rejected for this RFC — every `CheckType` examined in §4.3 has at most one severity-relevant raw number; a generic schema is speculative complexity for a need that doesn't exist yet. Revisit if a future check type genuinely needs more than one comparable number.

## 8. Risks

- **Silent behavior change for existing checks with `Degraded*`/`Warning*Before*` fields already configured.** A team that set `WarningDaysBeforeExpiry: 30` on an SSL check today, expecting DEGRADED at 30 days, will see that stop happening the moment Phase 3 (SSL) ships — unless they've also created the equivalent `AlertConfig: CertExpiry, AlertValue: "30", Severity: Warning`. This needs an explicit migration-time action, not just a schema change: either (a) a one-time data migration that reads each check's existing threshold fields and auto-creates the equivalent `AlertConfig` rows before the fields are removed, or (b) a release-notes callout loud enough that self-hosters check their config before upgrading. Given this project's stated preference for explicit human action over silent auto-behavior (RFC 0001 §3, Incident promotion), (a) run as a one-time migration seems more consistent than relying on (b) alone.
- **`AlertFor.Uptime` removal breaks any existing `AlertConfig` row that has it set** (even though it's inert today — `IsThresholdConditionMet`'s fallthrough always returns `false` for it). If any such row exists in a deployed instance, the enum-to-string EF conversion (`AlertConfiguration.cs:20` pattern, if `AlertFor` also uses `HasConversion<string>`) needs confirming before removal — an unmapped stored string could throw on deserialization rather than just failing to match, which would be a worse failure mode than "alert never fires."
- **Interaction with RFC 0001's `CheckType.Webhook`** (tracked in issue [#162](https://github.com/Heva-Co/piro/issues/162)): RFC 0001 §4.2 states "`Check.CurrentStatus` is still assigned directly (here, by the webhook instead of an executor)" and §4.5 explicitly says "resolve its `AlertConfig` (**one per Check, as already enforced today**)" — both assumptions this RFC changes. Concretely: (1) an Alertmanager-originated `Webhook` check assigns `Check.CurrentStatus` directly from the mapped severity (`critical→DOWN`, `warning→DEGRADED` per RFC 0001 §4.5) — under this RFC's principle, that's still fine, because a `Webhook` check *by definition* has no executor of its own to separate "measure" from "judge": the severity arrives pre-judged from Alertmanager, which already did its own PromQL threshold evaluation. `Webhook` should be treated as an intentional exception to §4.5's "executors only report UP/DOWN" rule, not a violation of it — worth stating explicitly in RFC 0001 if it's revised, so a future reader doesn't think the two RFCs contradict each other. (2) RFC 0001's "one `AlertConfig` per Check" language should be read as "one is sufficient for the minimal Phase 1 webhook case," not a hard requirement — once this RFC's N:1 change ships, a `Webhook` check could reasonably have two `AlertConfig`s too (e.g. separate `AlertFor: Status` rows for `warning` vs `critical` label values), which is a strict improvement, not a breaking change to RFC 0001's design.
- **`GCP_CloudRunJob`'s `MaxAgeHours` staying on the check (§4.3) is a judgment call, not a clean application of the design principle** — it's arguably still "the check deciding severity," just dressed as "what to measure." Flagged in §4.3 as a recommendation, not a certainty; worth revisiting during Phase 3 implementation if a cleaner split emerges (e.g. moving to `AlertFor: Status` comparing `MetricValue` = hours-since-last-run against an `AlertConfig`-owned threshold, mirroring SSL exactly, if two-tier staleness alerting turns out to matter in practice).
