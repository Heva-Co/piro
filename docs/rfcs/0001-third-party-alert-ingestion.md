# RFC 0001 — Third-party alert ingestion (GCP Cloud Monitoring)

Status: implemented (Phases 1–3; PR #173)
Author: Arael Espinosa (assisted draft)
Date: 2026-07-13 (revised 2026-07-14, 2026-07-15, 2026-07-16)

## 1. Problem

Piro checks services at L5-L7 (HTTP, TCP, DNS, Ping, SSL, GRPC) from the outside in. Two structural limits follow:

1. **Network reachability.** A check needs the target reachable from Piro's worker. Resources on a fully private network can't be checked without exposing them.
2. **Blindness to internal metrics.** A `200 OK` says nothing about disk/memory pressure, queue depth, or error rate on a specific endpoint. GCP Cloud Monitoring already solves this natively for GCP resources with metrics + alerting policies — Piro shouldn't reimplement threshold evaluation.

Cloud Monitoring already knows how to evaluate "is this metric bad enough to alert" — Piro should only receive the result.

## 2. Non-goals

- **Metrics pull** (the Cloud Monitoring `timeSeries` API) to evaluate thresholds ourselves — rejected, it duplicates work Cloud Monitoring's own alerting engine already does. Candidate for a separate RFC later.
- **Other third-party alert sources** (Alertmanager/Prometheus, Sentry, or anything without a notion of a monitored resource to correlate against) — out of scope here. The `Alert` model change below (§4.2) is general enough to support any of them later without further schema churn, but none of them ship in this RFC.
- **Webhook actions other than "create an Alert."** A future webhook that creates an `Incident` directly, or runs custom logic, is out of scope. There's no existing precedent in Piro for a generic/polymorphic action (`INotificationDispatcher` is a fixed 1:1 per `IntegrationType`; `EscalationStep` has a hardcoded target) — that would need its own RFC once there's a real use case to design against.
- **A general `Integration` capability manifest** — this RFC depends on one (§4.3); see [RFC 0003](0003-integration-manifest.md), already implemented and merged to `main`.

## 3. Design principle

**Don't reinvent the source platform's alerting pipeline.** Cloud Monitoring already evaluates its own alerting policies/conditions. Piro only:

- Receives what it already decided to send ([GCP webhook notification channel](https://docs.cloud.google.com/monitoring/support/notification-options#webhooks)).
- Translates it into `Alert` — never directly into `Incident`.

Promoting an `Alert` to an `Incident` (public status page + `INotificationDispatcher`) stays a manual admin decision (`POST /api/v1/alerts/{id}/incident`), exactly as it is today for internal alerts. The webhook creates the signal, not the incident.

## 4. Design

### 4.0 Relationship to Heartbeat ([issue #1](https://github.com/Heva-Co/piro/issues/1))

`CheckType.Heartbeat` (planned, unimplemented) is the same underlying pattern — a passive check driven by a push signal, not `ICheckExecutor`.

| | Heartbeat | This RFC |
|---|---|---|
| Signal | "still alive," no payload | explicit firing/resolved event with severity |
| Transitions to DOWN | on **absence** of a signal | on **presence** of a firing event |
| Needs a timeout evaluator | yes, essential | not essential, but useful to detect Cloud Monitoring going silent (§8) |

When Heartbeat ships, extract its "last-signal-received" evaluator as a shared piece instead of building a second one here.

### 4.1 Flow

```
GCP Cloud Monitoring (alerting policy)
        evaluates policy/condition
                          ↓
        POST webhook notification channel
                          ↓
        POST /api/v1/webhooks/gcp/{integrationId}
                          ↓
        WebhookRequestLog row written (§4.4) — captures every request, pass or fail
                          ↓
        validates auth (query-string token)
                          ↓
        resolves Check/Service via policy_name, IF configured (§4.2) — otherwise orphan
                          ↓
        AlertLifecycleService.RecordOccurrenceAsync (dedup by incident_id) — same path as internal alerts
                          ↓
        (no human involvement — automatic path ends here, Alert.IncidentId stays null)
                          ↓
        admin reviews the Alert, decides whether to promote it to an Incident (manual)
                          ↓
        promotion triggers the existing INotificationDispatcher
```

Piro never initiates a connection toward Cloud Monitoring — it only receives. The "don't reinvent the alerting engine" argument (§3) is what motivates this, not network reachability (Cloud Monitoring is already a public Google-managed service).

No notification fires just from a webhook arriving — only when an `Alert` is promoted to an `Incident` (or auto-promoted, Phase 4). This avoids Cloud Monitoring's flapping spamming notification channels without a human in the loop.

### 4.2 `Alert` decoupled from `Check` and `Service`

Today `Alert.CheckId`/`ServiceId` (`Alert.cs:14-15`) are non-nullable `int` with `OnDelete(Cascade)` FKs — no `Alert` can exist without a real `Check`+`Service`. An earlier revision of this RFC proposed a synthetic `CheckType.Webhook` anchor to work around that. **That anchor design is dropped** (full comparison in §7).

Reason: a GCP alert correlates to a real resource when the operator sets one up (a `policy_name` matching a Check's identifier) — but forcing that correlation to be *mandatory*, via a fake anchor Check, adds setup friction for no benefit when a team just wants to receive and triage alerts without modeling every GCP alert policy as a Piro Check. Nullable FKs let correlation stay **optional** instead of forced.

**Change**: `Alert.CheckId`/`ServiceId` become nullable, FKs move `Cascade` → `SetNull` (same pattern already used by `Incident`'s FK, `AlertEntityConfiguration.cs:41`, and by `Service.EscalationPolicyId`, `Service.cs:51-52`). This is additive:

- **Anchored alerts** (internal, or a GCP alert resolved via `policy_name`) — unchanged behavior, still participate in `Service.CurrentStatus` via `ServiceStatusService`.
- **Orphan alerts** (`CheckId = null`, `ServiceId = null`) — new. Don't run through `ICheckExecutor`, don't affect `Service.CurrentStatus` (`ServiceStatusService.cs:43-47` only ever reads `Check`, confirming an orphan Alert is structurally invisible to public status until a human promotes it), and need a manual Service pick at incident-promotion time (§4.9).

A GCP integration *may* still anchor to a Check if the team wants that correlation; it's no longer required to.

### 4.3 `GcpCloudMonitoringWebhook` integration type, its manifest, and escalation policy

Extend `IntegrationType` (`src/Piro.Domain/Enums/IntegrationType.cs`) with one value: **`GcpCloudMonitoringWebhook`**. Full name, not provider-only (`GoogleCloud` is already taken by an unrelated *outbound* integration, `GcpCloudRunJobCheckExecutor.cs:15`).

This RFC depends on [RFC 0003 — Integration manifest](0003-integration-manifest.md) (already merged to `main`, PR #172) to declare `GcpCloudMonitoringWebhook`'s manifest via `IntegrationManifestAttribute`: direction `Inbound`, capabilities `CreatesAlerts`/`SupportsEscalationPolicy`/`SupportsCheckCorrelation` (three new `IntegrationCapability` flags this RFC adds — RFC 0003 only shipped `SendsPersonalNotification`/`RequiredByCheckType`), and a `GcpCloudMonitoringWebhookConfig` class (`AuthToken` field, `[SecretField, ConfigField(...)]`) following the same pattern as the existing `JiraConfig`/`TwilioConfig`/etc. in `src/Piro.Domain/Integrations/Config/`.

`Integration` also gains `EscalationPolicyId` (`int?`), same nullable-FK shape as `Service.EscalationPolicyId`. This is how an orphan alert gets on-call escalation without a `Service` to inherit a policy from (§4.6).

### 4.4 Observability: `WebhookRequestLog` and `Alert` source metadata

Nothing today logs inbound webhook traffic. `PiroLogs` (Serilog → `EfCoreLogSink.cs`, queryable via `GET /api/v1/logs`) is general app logging, not scoped per-Integration. `EscalationDeliveryLog` is the closest shape precedent, but for *outbound* delivery.

**New `WebhookRequestLog` entity** — one row per inbound request, written *before* auth/parsing so failures are captured too:

| Field | Purpose |
|---|---|
| `IntegrationId` | which webhook |
| `ReceivedAt` | timestamp |
| `RawPayload` | exact request body, unmodified |
| `Outcome` | `Accepted` / `AuthFailed` / `CorrelationFailed` / `ParseError` / `Deduplicated` (exact set TBD) |
| `AlertId` (`int?`) | set only when the request produced/updated an `Alert` |

This answers "how many POSTs did this webhook get, what did they look like, how many became alerts" directly from the admin panel. `PiroLogs` stays for tracing exceptions (ideally tagged with `IntegrationId` so the two logs cross-reference).

**`Alert` gains**:

- `Source` (enum: `Internal`, `GcpCloudMonitoring`) — origin of the alert. Internal alerts get `Source = Internal`. Extensible for a future source without touching anything else.
- `SourceRequestLogId` (`int?`, FK to `WebhookRequestLog`) — instead of duplicating `RawPayload` onto `Alert`, it points at the exact request that created it. Admin alert-detail view joins through this to show the original JSON.

### 4.5 Alertmanager endpoint

Out of scope for this revision — see §2. If picked up later, would follow the same pattern as §4.8 (dedicated endpoint, own auth/payload/correlation), reusing the orphan-alert model from §4.2.

### 4.6 Translation into the domain model, and `Alert.EscalationPolicyId`

- **Firing** (`incident.state: open`) → resolve Check/AlertConfig via `policy_name` if configured, otherwise skip straight to orphan. Call `AlertLifecycleService.RecordOccurrenceAsync` — same call `AlertEvaluationService` makes internally. If a Check was resolved, `Check.CurrentStatus` updates and participates in `ServiceStatusService` as usual; an orphan alert has no status effect.
- **Resolved** (`incident.state: closed`) → same fingerprint, `AlertLifecycleService.ResolveActiveAlertAsync`.
- **`Alert.EscalationPolicyId` (new, `int?`) — snapshotted once at creation, never re-resolved live.** Copied from `Service.EscalationPolicyId` (anchored alerts) or `Integration.EscalationPolicyId` (orphan alerts).

  Why a snapshot: `EscalationCheckerService` ticks on a fixed interval against in-flight alerts. If the policy were read live, an admin editing the Integration's policy mid-escalation would silently change an alert already partway through different steps. Same reasoning as `ImpactAtFireTime`/`Message` already being frozen at fire time (`Alert.cs:20,24`).

  Bonus: this simplifies `AlertRepository.GetActiveWithServiceEscalationAsync` from `Where(a => a.Service.EscalationPolicyId != null)` to `Where(a => a.EscalationPolicyId != null)` directly on `Alert` — one less join, and it now naturally includes orphan alerts too.

  `EscalationCheckerService.cs`'s current unguarded reads of `alert.Service.Name`/`alert.Check.Name`/`.Slug`/`alert.Service.EscalationPolicy!` need to switch to `alert.EscalationPolicy` directly plus null-safe display fallbacks.

  Per-severity escalation rules within a single Integration are out of scope for this phase — flat model, one policy per Integration, copied verbatim.

- `Alert.IncidentId` stays `null` until manual promotion (§4.9) — no automatic `Incident` creation, ever.
- `INotificationDispatcher` doesn't fire on webhook receipt, only on Incident promotion — unchanged from today. On-call escalation delivery (`EscalationDeliveryLog`) is separate and already fires off `Alert.EscalationPolicyId` regardless of Incident state.

### 4.7 What does NOT change

- `ICheckExecutor` — untouched.
- `AlertConfig`, `AlertLifecycleService`, `AlertEvaluationService` — reused as-is; this webhook is another producer of "occurrences," not a parallel pipeline.
- `IncidentAppService`, `INotificationDispatcher` — untouched; promotion stays 100% manual.
- `Incident`'s existing multi-service design (`IncidentService` junction, nullable `TriggeringCheckId` for "added manually") — §4.9 reuses this as-is, it's not a new concept.
- `CheckDataPoint` — not applicable.

### 4.8 GCP Cloud Monitoring endpoint

`POST /api/v1/webhooks/gcp/{integrationId}`

- **Auth**: GCP's webhook channel only supports Basic Auth or `?auth_token=<secret>` query param — no HMAC, no custom headers. **Decision: query-string token** — must be scrubbed from any access/request logging. The Cloud Console's webhook dialog only has one "Endpoint URL" field, so the admin UX (Phase 3) should pre-build the full URL with the token embedded, ready to paste.
- **Payload**: GCP's [v1.2 webhook schema](https://docs.cloud.google.com/monitoring/support/notification-options#webhooks) — single `incident` object per request, with `incident_id`, `state`, `resource`, `resource_display_name`, `policy_name`, `severity`, etc.
- **Correlation** (optional): `policy_name`, not `resource.labels.*`. `resource.labels`' shape depends on `resource.type` (`cloud_run_revision` has `service_name`; `gce_instance` doesn't) — a mapping keyed to a label breaks the moment a policy covers a different resource type. `policy_name` is stable across all types and operator-chosen.
- **Idempotency**: `incident.incident_id` as `MessageFingerprint` — stable across the `open`→`closed` lifecycle and `renotify` repeats.
- **Severity mapping**: `critical→DOWN`, `warning→DEGRADED`, default `DEGRADED`.

<details>
<summary>Why not `resource_id` or `resource.labels.*` for correlation</summary>

`resource.labels`' shape depends entirely on `resource.type` (a `cloud_run_revision` has `service_name`; a `gce_instance` has `instance_id`/`zone` instead, no `service_name` at all) — a mapping keyed to one `resource.labels` field would silently stop working the moment a policy covers a different GCP resource type. The top-level `resource_id` field has the same problem: GCP's docs define it verbatim as "instance ID of the monitored resource, same value as `resource.labels.instance_id`" — i.e. it's defined in terms of a label that itself only exists for `gce_instance`-type resources, with no documented value for a `cloud_run_revision` or other non-VM resource. `policy_name` is the one field that's both stable across every resource type and deliberately operator-chosen (set when creating the alert policy in GCP), the same intent as a correlation label an operator would set on a Prometheus rule. Should be confirmed empirically against a real Cloud Run-triggered payload during Phase 2 implementation to validate `resource_id`'s actual value before ruling it out with full certainty.
</details>

### 4.9 Promoting an orphan `Alert` to an `Incident`

`AlertAppService.LinkToIncidentAsync` calls `IncidentAppService.CreateAlertIncidentAsync(title, ct)` — which already takes **no `ServiceId` parameter**. The caller attaches the Service afterward as an `IncidentService` row, today read from `alert.ServiceId`. Since `Incident` already supports multiple services via that junction table (`TriggeringCheckId` nullable, documented as "added manually"), the mechanism for a Service with no triggering Check **already exists** — this only needs a new caller.

**Gap**: that attachment step assumes `alert.ServiceId` is always present. Fix: extend `LinkAlertToIncidentRequest` with an optional `ServiceIds`, used only when `alert.ServiceId is null`:

- `alert.ServiceId` set → unchanged behavior.
- `alert.ServiceId is null` + `ServiceIds` provided → insert one `IncidentService` row per Service, `TriggeringCheckId = null`.
- `alert.ServiceId is null` + no `ServiceIds` → reject with a validation error (a Service-less Incident has no defined public meaning today).

Only affects the manual promotion endpoint — nothing about automatic alert creation changes.

## 5. Data / schema scope

- `IntegrationType`: add `GcpCloudMonitoringWebhook`, with a manifest declared via `IntegrationManifestAttribute` (RFC 0003) — direction `Inbound`, capabilities as in §4.3, and a `GcpCloudMonitoringWebhookConfig` class.
- `IntegrationCapability` (RFC 0003 enum, `src/Piro.Domain/Enums/IntegrationCapability.cs`): add `CreatesAlerts`, `SupportsEscalationPolicy`, `SupportsCheckCorrelation` — declared in the RFC 0003 design but not part of the flags that actually shipped there; this RFC adds them.
- `Integration`: add `EscalationPolicyId` (`int?`).
- `Alert`:
  - `CheckId`/`ServiceId`: `int` → `int?`, FKs `Cascade` → `SetNull`.
  - Add `EscalationPolicyId` (`int?`) — snapshot, not live-resolved.
  - Add `Source` (enum) and `SourceRequestLogId` (`int?`, FK to `WebhookRequestLog`).
- New entity `WebhookRequestLog` (§4.4).
- `AlertSummaryDto`/`AlertDetailDto`: `CheckSlug`/`CheckName`/`ServiceSlug`/`ServiceName` → nullable — ripples into OpenAPI types consumed by `apps/admin`.
- `LinkAlertToIncidentRequest`: add optional `ServiceIds`.
- **No new `CheckType`** — the `CheckType.Webhook` anchor idea is dropped (§7). Anchoring, where wanted, uses an existing passive `CheckType` shape.
- No changes to `AlertConfig`, `CheckDataPoint`, `Incident`/`IncidentService` schema.

**Code changes beyond schema** (not exhaustive): null-safety in `EscalationCheckerService.cs`'s reads of `alert.Service`/`alert.Check`; `AlertRepository.cs` projections (`GetPagedSummaryAsync`, `GetDetailByIdAsync`) and the simplified escalation query; `AlertLifecycleService.RecordOccurrenceAsync`'s signature to accept nullable Check/Service; `AlertAppService.cs`'s incident-linking logic; `apps/admin`'s `AlertsPage.tsx`/`AlertDetailPage.tsx`, which today read `alert.checkSlug`/`checkName`/`serviceSlug`/`serviceName` with **zero null-guards** (`AlertsPage.tsx` calls `.toLowerCase()` on `checkName` in a search filter — `TypeError` on an orphan alert as-is). Replicate the one existing null-safe pattern in the codebase: `IncidentExtensions.cs`'s `a.Check?.Slug ?? a.CheckId.ToString()`.

## 6. Phased plan

1. **`Alert` decoupling** — nullable FKs, `EscalationPolicyId`, `Source`/`SourceRequestLogId`, `WebhookRequestLog`, all null-safety fixes. No new ingestion yet; existing alerts behave identically (still non-null in practice).
2. **GCP Cloud Monitoring ingestion** — endpoint, query-token auth, `incident_id` dedup, `policy_name` correlation.
3. **Admin UX** — create the integration, show the token once, optional anchor Check, configure `EscalationPolicyId`, pre-built webhook URL, `WebhookRequestLog` viewer.
4. **Optional auto-promotion** — `severity → auto-promote` mapping per integration, opt-in only. Orphan alerts need a default target Service configured for this to work.
5. **(Out of scope) Metrics pull** — the `timeSeries` API as a `CheckType`, if a real need for it shows up.
6. **(Out of scope) Additional sources** — Alertmanager/Prometheus, Sentry, or any source with no correlatable resource — a follow-up RFC, reusing the orphan-alert model from §4.2 as-is.

## 7. Alternatives considered

- **Piro as a Prometheus/Cloud Monitoring exporter**: a different problem (observability of Piro itself), doesn't solve the motivating case.
- **Metrics pull per check** — see §2.

### 7.1 Anchor `Check` vs. nullable `Alert.CheckId`/`ServiceId` — reversed decision

An earlier revision picked the anchor-Check approach. That's reversed here.

| | Anchor Check | Nullable `Alert.CheckId`/`ServiceId` (this revision) |
|---|---|---|
| Migration risk | None | New migration, FKs `Cascade`→`SetNull` |
| Code changes | None | Null-safety across `EscalationCheckerService`, `AlertRepository`, DTOs, admin UI |
| `Service.CurrentStatus` | Automatic via the anchor | None for orphan alerts — by design |
| Escalation without a Service | N/A, anchor always has one | `Integration.EscalationPolicyId` snapshot |
| Setup friction for teams that just want to receive/triage alerts without modeling every GCP alert policy as a Check | High — a fake Check per Service × policy is mandatory | None — correlation becomes optional |

The earlier tradeoff ("anchor's cost is one-time setup, nullable's cost is a standing maintenance burden") undersold the setup cost and treated the null-safety work as avoidable overhead. It isn't avoidable long-term: `AlertRepository`, `EscalationCheckerService`, and the admin UI need this null-safety eventually regardless, the moment any future alert source doesn't cleanly map to one Check per Service (§6, out-of-scope sources). Doing it now, once, is preferable to shipping a mandatory anchor and revisiting this same tradeoff later.

## 8. Risks

- **Correlation misconfiguration silently falls back to orphan** instead of failing — worth an internal signal for "webhook alert went orphan due to missing `policy_name` match" so a config mistake is distinguishable from a deliberate choice not to anchor.
- **Resend storms**: GCP resends with `renotify: true` on an unspecified schedule. Handled by existing `incident_id`-based dedup in `AlertLifecycleService`.
- **Orphan alerts are easy to lose track of** in the admin UI — no Service page to surface on. Needs a dedicated filter/count badge (Phase 3), not just the flat alerts list.
- **An anchored passive Check never looks "green" on its own** if Cloud Monitoring stops sending resolutions — no heartbeat of its own. Worth a TTL/staleness check during implementation. Only applies to the optional anchored case.
- **Secret exposed in logs**: the query-string auth token is GCP's only option besides Basic Auth — it must be explicitly scrubbed from any access/request logs, not just application-level logging.
- **GCP payload shape depends on `resource.type`** — see §4.8 detail box. Any code reading `resource.labels.*` directly will break silently for untested resource types.
- **`Alert.EscalationPolicyId` snapshot can drift from the Integration's/Service's current policy** — intentional (determinism over live-reconfiguration), but should be called out in the admin UX so editing a policy is understood to affect only future alerts.

## 9. References

- [GCP Cloud Monitoring — webhooks](https://docs.cloud.google.com/monitoring/support/notification-options#webhooks) — payload schema, authentication mechanisms, Console setup steps.
- [GCP — `timeSeries.list`](https://cloud.google.com/monitoring/api/ref_v3/rest/v3/projects.timeSeries/list) — relevant if §6 metrics-pull is picked up.
- [Google Managed Service for Prometheus](https://cloud.google.com/stackdriver/docs/managed-prometheus) — rejected alternative for a hypothetical pull-based check.
- [RFC 0003 — Integration manifest](0003-integration-manifest.md) — the capability-declaration model this RFC's `GcpCloudMonitoringWebhook` depends on, already merged (PR #172).
