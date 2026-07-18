---
rfc: 4
title: "OAuth integration framework with resource discovery (PagerDuty as first consumer)"
status: implemented
created: 2026-07-16
depends-on: ["0001", "0003"]
proposal-pr: null
implementation-pr: 193
---

# RFC 0004 — OAuth integration framework with resource discovery (PagerDuty as first consumer)

Status: proposal
Author: Arael Espinosa (assisted draft)
Date: 2026-07-16

## 1. Problem

`IntegrationType.PagerDuty` (`src/Piro.Domain/Enums/IntegrationType.cs:59-68`) is a fully creatable integration type — complete manifest, label "PagerDuty", a `PagerDutyConfig` with a required `RoutingKey` — yet an admin who creates it and saves a Routing Key gets nothing: no `PagerDutyDispatcher` exists in `src/Piro.Infrastructure/Alerts/`, nothing is registered for it, and no code path would ever send an event to PagerDuty. Making PagerDuty actually work surfaces three distinct gaps, each larger than the last:

1. **No shared-channel dispatch.** `INotificationDispatcher` (`src/Piro.Application/Interfaces/INotificationDispatcher.cs:8-28`) targets one individual's *personal handle* (a Telegram chat ID, a phone number). PagerDuty authenticates a whole team service via a Routing Key — there is no per-user handle. The codebase already recognizes this: `PersonalNotificationChannel.cs:5-10` names PagerDuty as a channel that "posts to a shared team channel" and is "not meaningful as a 'notify me personally' option." No interface for shared-channel, lifecycle (trigger/resolve) dispatch exists today.

2. **No link between a `Service` and a shared outbound channel.** A `Service` links only to on-call *people*, via `Service.EscalationPolicyId` (`Service.cs:49-52`) → schedules → users → personal preferences. `Check.IntegrationId` (`Check.cs:49-53`) is inbound/execution config (credentials a check *executor* needs, e.g. a GCP service-account JSON), never consulted when notifying on failure. The only code that dispatches on an alert — `EscalationCheckerService` (`EscalationCheckerService.cs:176-228`), a Quartz job running every minute — iterates `UserNotificationPreference` rows per user; it has no notion of "notify this Service's PagerDuty."

3. **Manual per-service Routing Key configuration doesn't scale, and is the real motivating problem.** Each PagerDuty *service* has its own Routing Key. A team with dozens of PagerDuty services would have to create an Integration in Piro and copy-paste a Routing Key for each, one by one, and keep them in sync by hand. The friction-free alternative — connect once, let Piro discover the services and their keys — requires Piro to authenticate against PagerDuty's REST API as an OAuth client and read each service's integration key. That is not a dispatcher feature; it is an **OAuth-client integration framework with remote-resource discovery** that Piro does not have as a reusable capability today, even though it has two partial, single-purpose OAuth flows to build on (§4.2).

This RFC is therefore framed around the largest gap (3): a **generic OAuth integration framework** that lets Piro connect to a third party, hold and refresh its tokens securely, and discover remote resources — with **PagerDuty as the first consumer** that exercises the whole chain end-to-end (connect → discover services + routing keys → dispatch trigger/resolve on check failure). GitHub and Jira are named throughout as the second and third plausible consumers, used to check that the "generic" parts are genuinely generic and not PagerDuty-shaped.

## 2. Non-goals

- **A generic *resource-discovery* abstraction.** The generic pillars are the OAuth *flow* (connect/callback/PKCE, encrypted token storage, refresh — §4.3) and the *mapping table* that records "this Piro Service ↔ that remote entity" (§4.5). What is deliberately **not** generic is the *discovery logic* — how each provider enumerates its remote entities and what those entities even are (PagerDuty: services + routing keys; GitHub: repos; Jira: projects). Those are too dissimilar to hide behind one shared `IResourceDiscoverer` interface derived from a single real consumer; forcing a common shape now would be abstraction-by-guessing. Each provider implements its own discovery (§4.4) and its own typed mapping class (the *contents* of `MappingJson`), while reusing the generic mapping table unchanged. See §7 for the rejected generic-discovery alternative.
- **Personal/individual PagerDuty notification.** PagerDuty has no per-user handle (§1); it is deliberately excluded from `PersonalNotificationChannel` and stays that way. Its `INotificationDispatcher` methods are documented no-ops (§4.7).
- **Triggering events via OAuth/REST.** Research confirmed (and this is load-bearing) that PagerDuty's Events API v2 `enqueue` endpoint authenticates *solely* via the `routing_key` in the payload — there is no OAuth-authenticated trigger endpoint. OAuth/REST is used only to *discover or provision* routing keys; the actual trigger/resolve still POSTs to `events.pagerduty.com/v2/enqueue` with a key (§4.6). OAuth does not replace the key.
- **PagerDuty's `acknowledge` action.** The Events API supports `trigger`/`acknowledge`/`resolve`; this RFC covers `trigger` and `resolve` only. Piro has no acknowledged-but-not-resolved alert state to fire `acknowledge` from — adding the method would be a dead stub. Small additive change later if that state ever exists.
- **GitHub and Jira implementations.** Named as validation cases for the generic OAuth flow's shape; not implemented here. (Note: `JiraConfig` today uses email + API token, not OAuth; GitHub SSO login exists but is not a resource-discovery integration — see §4.2.)
- **Inbound PagerDuty → Piro state sync** (reflecting a human's acknowledge/resolve in PagerDuty back onto the Piro `Alert`, via PagerDuty Webhooks v3). Same principle as RFC 0001 §3 — PagerDuty owns its incident state once created. A future RFC once there's a concrete two-way-sync need.
- **A phased rollout that ships paging before OAuth.** An Events-API-first phase (manual Routing Key, working paging early, OAuth later) was considered and explicitly rejected in favor of a single OAuth-complete delivery — see §6 and the §7 alternative. This is a deliberate big-bang choice: no functional paging until the framework, discovery, platform link, and dispatcher all land.

## 3. Design principle

Three constraints shape every choice in §4:

1. **Piro tells the third party *what happened*; the third party decides *who to wake up*.** PagerDuty owns escalation policies, on-call schedules, acknowledge/resolve, and repeat-until-answered paging. Piro's job is only to open an event on check failure (`trigger`) and close it on recovery (`resolve`). This mirrors RFC 0001's "don't reinvent the source platform's alerting pipeline," in the sending direction.

2. **Generalize the *shape* that recurs; keep provider-*specific* what genuinely differs.** Three things recur across any OAuth-discovery integration and are therefore generic: the OAuth flow (already half-built as `OidcService`'s authorization-code + PKCE), the token store (generalizing `GcpTokenProvider`'s per-integration token + cache into a persistent, encrypted one), and the mapping table that records "Piro Service ↔ remote entity" (§4.5, in the same `jsonb`-blob-with-typed-class model Piro already uses for `Integration.ConfigJson`). What genuinely differs per provider — the discovery logic and the *contents* of the mapping — stays specific. The line is drawn at recurring *shape* (generic) vs. per-provider *content/behavior* (specific), validated against GitHub/Jira (§2, §7).

3. **OAuth done properly includes its security surface.** A framework that holds *refresh tokens* — long-lived credentials that can mint access to a third party — cannot store them the way `ConfigJson` stores a static key today (plaintext `jsonb`, no rotation). Encrypted-at-rest storage and a persistent, refreshing token store are intrinsic to the framework, not a separate concern (§4.3, §8).

## 4. Design

### 4.1 End-to-end flow

Everything below **[new]** does not exist today. Steps not marked reuse existing code.

```
CONNECT  [new framework — §4.3]
  Admin clicks "Connect PagerDuty" on the integration
        → Piro (OAuth client) redirects to PagerDuty authorize endpoint
          (authorization code + PKCE, scopes: services.read [+ write to provision])
        → PagerDuty callback → Piro exchanges code for access + refresh tokens
        → tokens stored ENCRYPTED in a persistent token store (§4.3), keyed by integration

DISCOVER  [new provider-specific — §4.4]
  Piro calls PagerDuty REST API v2 with the OAuth bearer token:
        GET /services?include[]=integrations  → each Service + its Events API v2 integration_key
        (or POST /services/{id}/integrations to provision a fresh key)
        → admin matches Piro Services ↔ PagerDuty Services (the routing key is stored per mapping row)

LINK  [new platform — §4.5]
  A Piro Service is linked to one or more shared-channel Integrations
  (ServiceIntegrationMapping rows; the PagerDuty routing key is stored in the row's MappingJson)

TRIGGER  [new handler on RFC 0009's engine — §4.6, new dispatcher — §4.7]
  A Check crosses FailureThreshold → AlertLifecycleService creates the Alert
        → RFC 0009 publishes AlertOpenedEvent → durable outbox
        → SystemEventHandler (new) loads the Alert, builds its context, and for each
          ServiceIntegrationMapping of the Alert's Service (Integration = SendsAlertEvents):
              ISystemEventDispatcher.TriggerAsync(mapping, context)
        → PagerDutyDispatcher POSTs events.pagerduty.com/v2/enqueue
          with event_action:"trigger", the discovered routing_key, a deterministic dedup_key
        → 202 → dedup_key persisted on the Alert  [new column — §5]

RESOLVE  [new handler on RFC 0009's engine — §4.6]
  The Check recovers → the Alert resolves → RFC 0009 publishes AlertResolvedEvent
        → SystemEventHandler reads the stored dedup_key(s)
        → ISystemEventDispatcher.ResolveAsync(mapping, stored dedup_key)
        → PagerDuty auto-closes its incident
```

Entity mapping — deliberately minimal:

| Piro | PagerDuty | Via |
|---|---|---|
| An `Alert` fires (a `Check` crosses `FailureThreshold`) | An `Event` (`event_action: trigger`) → PagerDuty Alert | Events API + discovered routing key |
| That `Alert` resolves (`Check` recovers) | `Event` (`event_action: resolve`, same `dedup_key`) | Events API |
| A Piro `Service` | A PagerDuty *service* | A `ServiceIntegrationMapping` row (routing key discovered via OAuth/REST, stored in `MappingJson`) |
| Multiple failing Checks on one Service | Multiple PagerDuty Alerts, grouped into Incidents by **PagerDuty's own** Alert Grouping | one `trigger` per Piro Alert; Piro does not group |

Piro sends one `trigger` per Piro `Alert` (i.e. per Check that crosses its threshold) and lets PagerDuty group Alerts into Incidents by the team's own Alert Grouping rules — Piro does not attempt grouping (that would reimplement PagerDuty, per §3).

### 4.2 What Piro already has (the foundation this generalizes)

The generic framework is not greenfield — it consolidates two existing, working patterns:

- **Authorization-code + PKCE OAuth client** — `OidcService` (`src/Piro.Application/Services/OidcService.cs`) + `OidcController` already implement the full flow for SSO login (Google/GitHub/Microsoft): `.well-known` discovery, PKCE `state`/`verifier`/`challenge` generation, PKCE-state caching in `IDistributedCache` (10-min TTL), authorization URL construction (`response_type=code`, `code_challenge_method=S256`), and code-for-token exchange (`OidcService.cs:224-298`). Its per-provider config entity `OidcProviderConfig` (`src/Piro.Domain/Entities/OidcProviderConfig.cs`) holds `Authority`/`ClientId`/`ClientSecret`/`RedirectUri`/`Scopes` — the exact shape a third-party OAuth-app registration needs. **Gap:** it uses the access token *once* (to fetch userinfo) then discards it — it never persists or refreshes third-party tokens.
- **Per-integration token provider + cache** — `GcpTokenProvider` (`src/Piro.Infrastructure/Integrations/GoogleCloud/GcpTokenProvider.cs`) reads credentials from an integration's `ConfigJson`, mints a bearer token (JWT-bearer grant), caches it by `IntegrationId` (`GcpTokenCache`, an in-memory `ConcurrentDictionary`), and hands it to a real remote REST call (`GcpCloudRunJobCheckExecutor.cs:50-73`). **This is the closest working precedent** to "Piro authenticates and calls a third-party API." **Gaps:** the cache is in-memory/ephemeral (lost on restart), and the grant is JWT-bearer, not authorization-code-with-refresh.

The framework = `OidcService`'s flow generalized to *persist and refresh* long-lived tokens, using `GcpTokenProvider`'s "token provider handed to an HTTP call" shape, with a *persistent, encrypted* token store replacing GCP's in-memory cache.

`JiraConfig` (email + API token) and GitHub SSO are **not** current precedents for OAuth resource-discovery — they're the future consumers this design is checked against, not existing implementations.

### 4.3 Generic OAuth framework

Scope of "generic" (per §2, the flow only):

- **`IOAuthClient`** — a provider-agnostic service that: builds the authorization URL (authorization code + PKCE), handles the callback and code-for-token exchange, and refreshes an access token from a stored refresh token (`grant_type=refresh_token`). Generalizes `OidcService`'s existing methods; providers supply only their endpoints and scopes.
- **Provider registration** — a per-provider OAuth-app config (`client_id`, `client_secret`, redirect URI, scopes, authorize/token endpoints), following the `OidcProviderConfig` DB-row precedent (admin-managed, not appsettings). One row per provider (PagerDuty, later GitHub/Jira).
- **Persistent, encrypted token store** — a new persisted entity holding `{ integrationId, accessToken, refreshToken, expiresAt, scopes }`. Tokens are **encrypted at rest** via `IDataProtector` — `AddDataProtection()` is already registered (`InfrastructureServiceExtensions.cs:65`) but currently unused for secrets, so this is a cheap, already-wired foundation rather than new infrastructure. This deliberately does **not** reuse the plaintext-`jsonb` `ConfigJson` model (`Integration.cs:12`, `IntegrationConfiguration.cs:17`) for refresh tokens (§8).
- **Token refresh — proactive background + defensive lock (not naive on-use).** A naive "refresh if near expiry on every use" is a production bug: refresh tokens rotate (PagerDuty invalidates the old refresh token when it issues a new one), so two concurrent dispatches that both see an expired token both refresh with the same refresh token — the second gets `invalid_grant`, and a late writer can clobber the good rotated token with a stale one. The design avoids the race in the hot path entirely:
  - **Proactive background refresh** — a Quartz job (Piro already runs Quartz, e.g. `EscalationCheckJob` every minute) refreshes tokens *before* they expire, off the dispatch path. When an alert fires, the access token is already fresh — no refresh happens in the critical section, so the concurrency simply doesn't arise there. Read `expires_in` at runtime (PagerDuty's Scoped-OAuth lifetime is provider-defined, not a reliable fixed number) and refresh at a margin before it.
  - **Defensive distributed lock** — for the residual cold-start case (a token just created, or the background job hasn't run yet) where an on-demand refresh is unavoidable, serialize it with a per-integration lock in `IDistributedCache` (which Piro already uses — `OidcService` caches PKCE state there). The lock holder refreshes and persists; waiters re-read the rotated token instead of refreshing again. This makes refresh correct across multiple Piro instances (multi-region Worker), which an in-process `SemaphoreSlim` alone would not.

What stays provider-specific and lives outside this generic core: scopes requested, the discovery calls, and what the discovered resource is used for (§4.4).

### 4.4 PagerDuty discovery (provider-specific)

Using the OAuth bearer token from §4.3 against PagerDuty REST API v2 (which accepts `Authorization: Bearer <token>` interchangeably with an API token):

- **List services + keys**: `GET /services?include[]=integrations` (or `GET /services/{id}/integrations/{iid}`) returns each service's Events API v2 integration object, which exposes `integration_key` — the routing key Piro needs. Confirmed against PagerDuty's official `go-pagerduty` SDK (`Integration.IntegrationKey json:"integration_key"`) and Terraform provider.
- **Optionally provision**: `POST /services/{id}/integrations` with `type: "events_api_v2_inbound_integration"` creates a fresh Events API v2 integration and returns its `integration_key` — so Piro can auto-provision a dedicated "Piro" integration on a PagerDuty service instead of reusing an existing one (cleaner separation; the requires `services.write` scope). Whether v1 provisions or only reads is a §6 implementation call.
- **Scopes**: `services.read` for listing; `services.write` if provisioning. Verify the exact create-scope string against the live app-registration scope picker at implementation time (research confidence was High for `services.read`, Medium for the create scope).
This whole subsection is what a GitHub or Jira consumer would replace wholesale (repos / projects), while reusing §4.3 unchanged — the validation that the generic/specific boundary holds.

### 4.4a Discovery API and the match flow (backend-driven UI)

The discovery result is **fetched live, not persisted** — the source of truth for "what PagerDuty services exist" is PagerDuty, and caching a stale list invites drift (a service renamed or deleted in PagerDuty). Only the admin's *chosen* mapping is persisted (in `ServiceIntegrationMapping`, §4.5). This mirrors the backend-classifies-frontend-places pattern RFC 0012 §4.3 establishes: the server answers "what remote resources can this integration see?" and the frontend renders the match UI; the server never encodes layout.

- **Discovery endpoint** — `GET /api/v1/integrations/{id}/discover` on `IntegrationsController` (existing `[Route("api/v1/integrations")]`, `[Authorize(Roles="Owner,Admin")]` — no new controller/auth surface, same as RFC 0012 §4.4). It resolves the integration's OAuth token (§4.3), runs the provider's discovery (§4.4, `GET /services?include[]=integrations`), and returns:

```csharp
public record DiscoveredResourceDto(
    string RemoteId,          // PagerDuty service id "PXXXXXX"
    string Label,             // service name, for the admin to recognize
    string? RoutingKey);      // the Events API v2 integration_key, if one already exists
```

  A provider that must *provision* a key (no existing Events API v2 integration on that service) returns `RoutingKey: null`; the match step (below) provisions on confirm. Returns `409`/a `requiresConnection`-style flag (RFC 0012 §4.4 pattern) if the integration isn't OAuth-connected yet, so the UI shows "Connect PagerDuty first" rather than an empty list.

- **Match endpoint** — `POST /api/v1/integrations/{id}/mappings` with `{ serviceId, remoteId }`. The backend re-reads (or provisions, if `services.write`) the `integration_key` for `remoteId`, builds the provider-typed `PagerDutyMapping { routingKey, pagerDutyServiceId = remoteId }`, and upserts a `ServiceIntegrationMapping` row (§4.5) with that `MappingJson`. Idempotent per `(ServiceId, IntegrationId)`. A `DELETE` on the same route unlinks.

- **Match UI** (`apps/admin`) — a screen listing Piro Services on one side and the discovered PagerDuty services (from the discovery endpoint) on the other, letting the admin pick a remote service per Piro Service. It reuses RFC 0012's backend-driven-descriptor convention (fetch descriptors, render from them) and the OpenAPI-generated-types discipline (`api-types.ts`), rather than hand-writing the wire shape. Detailed component layout is deferred to implementation (the same `apps/admin` conventions RFC 0012 §4.7 uses); what this RFC fixes is the **contract** (the two endpoints + the DTO + live-not-cached discovery + persist-only-the-match), which was the real gap.

- **Re-discovery / drift** — because discovery is always live, a service deleted in PagerDuty simply stops appearing; an existing `ServiceIntegrationMapping` pointing at a now-dead `remoteId` is surfaced (the stored `pagerDutyServiceId` no longer matches any discovered resource) so the admin can remap. Dispatch (§4.6) uses the *stored* routing key and does not re-discover per alert — only the match UI hits the discovery endpoint.

### 4.5 Linking + mapping a Service to Integrations (`ServiceIntegrationMapping`)

This is the fourth generic pillar (alongside the OAuth flow, token store, and the discovery *call shape*). A single **many-to-many** table both *links* a `Service` to an `Integration` and *maps* it to the remote entity that integration will act on — because those are the same row: the link exists precisely to record "this Piro Service corresponds to that remote thing."

```
Service ──┐  ServiceIntegrationMapping
          ▼    · ServiceId      (FK → Service,     OnDelete Cascade — local, typed)
  ServiceIntegrationMapping      · IntegrationId   (FK → Integration, OnDelete Cascade — local, typed)
          ▲    · MappingJson    (remote coordinates — provider-specific blob, NO FK)
      Integration
```

**Why the mapping is a blob, not typed columns.** The mapping stores *identifiers of entities that live in the third party*, not in Piro's database — so there is nothing to foreign-key to. Its shape differs per provider, and adding a provider must not require a schema change. This is the exact model Piro already uses one level up for `Integration.ConfigJson` (a provider's credentials in a `jsonb` blob, with a typed C# class per provider deserialized and validated from it) — the mapping applies the same pattern one level down, per Service×Integration pairing. Each provider defines a typed mapping class validated on deserialize (mirroring the dispatchers' existing `JsonUtils.DeserializeAndValidate<T>` usage):

```jsonc
// PagerDuty  — PagerDutyMapping
{ "routingKey": "R1a2b3c4...",     // the Events API v2 key used to trigger (functional)
  "pagerDutyServiceId": "PXXXXXX" } // the PD service id (for display / re-discovery)

// Jira (future) — JiraMapping
{ "projectKey": "OPS", "issueType": "Incident", "jiraProjectId": "10001" }

// GitHub (future) — GitHubMapping
{ "owner": "heva-co", "repo": "piro", "repoId": 123456789 }
```

- The two FKs (`ServiceId`, `IntegrationId`) are to Piro-local entities and stay typed; only the *remote coordinates* are a blob. This is the generic/specific line applied to the mapping: the **table** is generic, its **contents** are provider-specific.
- One PagerDuty *account* Integration can serve many Piro Services, each row carrying a different `routingKey` in its `MappingJson` — the reason the mapping rides on the link, not on the Integration.
- Only Integrations whose manifest declares `SendsAlertEvents` (§4.8) are offerable as link targets; the admin picker filters by that capability, reading the manifest as it already does for the creation picker.
- **Answering "what happens tomorrow when another integration needs the same thing?"** — a new provider adds only a typed mapping class and its discovery (§4.4); it reuses this table with zero schema change. That is the payoff of making the mapping generic instead of hanging a PagerDuty-shaped `RoutingKey` column on the link.
- **Why N:M, not a single `Service.AlertIntegrationId`**: a single FK forecloses "page PagerDuty *and* post to Slack" without a later migration (§7).
- **Why not fold into `EscalationPolicy`**: that model is timed steps that page *people*; PagerDuty runs its *own* timed escalation once triggered. A shared-channel trigger fires once, immediately, when the alert opens — not a step in Piro's people-paging ladder (§3, §7).

### 4.6 Where the trigger and resolve fire — via RFC 0009's notification engine

This RFC does **not** add its own hook into the alert-firing path. RFC 0009 (notification system revamp) already establishes the one integration point on the alert path — `AlertLifecycleService.RecordOccurrenceAsync` publishes `AlertOpenedEvent`/`AlertResolvedEvent` to a durable outbox drained by handlers (RFC 0009 §4). RFC 0009 explicitly names this RFC's "mode 3" (`trigger`/`resolve` with a dedup key) as belonging here and out of its own scope. So the wiring is:

- **A new `SystemEventHandler : INotificationEventHandler<AlertOpenedEvent>` (and `…<AlertResolvedEvent>`)** — registered on RFC 0009's engine, mirroring its `BroadcastHandler`. On `AlertOpenedEvent`, it loads the `Alert`, builds its `AlertNotificationContext` via `Alert.ToNotificationContext` (`AlertExtensions.cs:41-59`, the same helper `BroadcastHandler` uses), finds the `ServiceIntegrationMapping` rows for the alert's `Service` whose Integration declares `SendsAlertEvents`, and calls `ISystemEventDispatcher.TriggerAsync` for each — persisting each returned `dedup_key` (§5). On `AlertResolvedEvent`, it reads the stored `dedup_key`(s) and calls `ResolveAsync`.
- **Why this handler and not `BroadcastHandler` itself**: broadcast is fire-and-forget group posting (RFC 0009 mode 2); mode 3 is a stateful `trigger`/`resolve` with a persisted correlation token. They share the event source but not the delivery contract — a separate handler keeps each contract clean, exactly the mode split RFC 0009 §4.1 draws.
- **Durability/retry for free**: because this runs on RFC 0009's outbox (Pending → Processing → Done, with backoff), a `TriggerAsync` that fails transiently is retried by the engine — this RFC doesn't build its own retry loop for the dispatch, only the per-call `429`/`5xx` handling inside the dispatcher (§4.7).
- **`dedup_key`** — deterministic per Piro `Alert` (e.g. from `Alert.Id`), returned by `TriggerAsync` and persisted (§5), so `trigger` and its later `resolve` provably reference the same PagerDuty Alert even if the derivation formula changes.
- **Fan-out / partial failure** — a service mapped to several `SendsAlertEvents` Integrations dispatches to each independently; one failure must not block the others (the outbox retries the failed one).
- **Hard dependency**: this makes **RFC 0009 a prerequisite** of this RFC's dispatch wiring (the trigger has nowhere to fire from without 0009's event+outbox). The generic OAuth framework and discovery (§4.2–4.5) do **not** depend on 0009 and can land first; only the §4.6 dispatch wiring waits on it. Reflected in §6 and §8.

### 4.7 `ISystemEventDispatcher` and `PagerDutyDispatcher`

```csharp
namespace Piro.Application.Interfaces;

/// <summary>
/// Sends an alert lifecycle event (trigger / resolve) to a shared, team-wide channel via a
/// specific integration link — as opposed to INotificationDispatcher, which targets one
/// individual's personal handle.
/// </summary>
public interface ISystemEventDispatcher
{
    IntegrationType Type { get; }

    /// <summary>Opens an event for the alert context; returns a correlation token
    /// (PagerDuty dedup_key) to persist and pass to ResolveAsync, or null on failure.</summary>
    Task<string?> TriggerAsync(ServiceIntegrationMapping mapping, AlertNotificationContext context, CancellationToken ct = default);

    /// <summary>Closes the event previously opened under this correlation token.</summary>
    Task<bool> ResolveAsync(ServiceIntegrationMapping mapping, string correlationToken, CancellationToken ct = default);
}
```

- Takes the **mapping row** (not just the `Integration`) because the routing key lives in its `MappingJson` (§4.5); the dispatcher deserializes its provider-typed `PagerDutyMapping` from it.
- No `AcknowledgeAsync` (§2) — no Piro state to fire it from.
- `PagerDutyDispatcher` builds the Events API v2 payload (`routing_key` from the mapping's `MappingJson`, `event_action`, `payload.summary`/`source`/`severity`/`timestamp`, `client`/`client_url` linking back to Piro), POSTs `events.pagerduty.com/v2/enqueue`. Severity maps Piro→PagerDuty (`critical`/`error`/`warning`/`info`), defaulting to `warning`, reusing RFC 0001's mapping pattern. Follows `PushoverDispatcher.cs`'s template (`IHttpClientFactory` "piro-webhook" client, log non-2xx bodies). On `400` (bad key) it returns `null`/`false` and logs — it does **not** throw, so one bad integration can't break alert processing. On `429`/`5xx`, single retry with backoff honoring `Retry-After` (Events API is dynamically throttled, ~480/min/key baseline).

**`INotificationDispatcher` for PagerDuty — documented no-op** (same one-line pattern as `OpsgenieDispatcher.cs:15-19`): both `DispatchPersonalAsync`/`SendPersonalMessageAsync` return `Task.FromResult(false)`, kept only so type-keyed lookups over `IEnumerable<INotificationDispatcher>` stay well-defined, consistent with PagerDuty's exclusion from `PersonalNotificationChannel`.

### 4.8 Manifest and test updates

- Add `IntegrationCapability.SendsAlertEvents` (`1 << 7`) to `IntegrationCapability.cs:10-28`, documented `"Has a registered ISystemEventDispatcher for this IntegrationType."` Flip PagerDuty's manifest (`IntegrationType.cs:62`) from `IntegrationCapability.None` to `SendsAlertEvents` — **not** `SendsPersonalNotification`, which would be a lie (personal dispatch is a no-op, §4.7). **Bit coordination:** the enum today ends at `SupportsCheckCorrelation = 1 << 4` (`IntegrationCapability.cs:27`); RFC 0009 claims `1 << 5` (`SendsGroupNotification`) and RFC 0012 claims `1 << 6` (`ProvidesActions`), so this RFC takes `1 << 7` to avoid colliding with either. Whichever of the three lands first defines the lower bits; the others must be rebased onto the next free bit at implementation time.
- Register `services.AddScoped<ISystemEventDispatcher, PagerDutyDispatcher>()` and `services.AddScoped<INotificationDispatcher, PagerDutyDispatcher>()` (one class, two interfaces) in `InfrastructureServiceExtensions.cs`.
- `tests/Piro.UnitTests/IntegrationManifestTests.cs`: the existing `SendsPersonalNotificationCapability_MatchesActualDispatcherRegistrations` stays as-is (PagerDuty correctly absent from `RegisteredDispatcherTypes`). Add a symmetric `SendsAlertEventsCapability_MatchesActualDispatcherRegistrations` against a new `RegisteredAlertEventDispatcherTypes = [IntegrationType.PagerDuty]` set, so the "manifest can't lie" guarantee covers the new capability too.

### 4.9 What does NOT change

- `INotificationDispatcher` and its five real implementations (Telegram/Twilio/Pushover/Ntfy/Email) — untouched; this adds a sibling interface.
- `EscalationCheckerService`'s per-user loop (`EscalationCheckerService.cs:176-228`) — untouched. System-event dispatch fires from a handler on RFC 0009's notification engine (§4.6), independent of the people-paging ladder. This is the load-bearing "don't reimplement PagerDuty's escalation" boundary (§3).
- `EscalationPolicy`/`EscalationStep`, `Check.IntegrationId`, `UserManagementService`'s verification flow — all untouched and unrelated.
- `OidcService`/`OidcController`/`OidcProviderConfig` — **not modified**; the generic framework generalizes their *pattern* into new services, it does not alter the SSO login path. (Whether to later refactor SSO onto the shared framework is out of scope.)
- `ConfigJson`'s plaintext model — untouched for existing integrations; the new encrypted token store is separate and additive (§4.3).
- Opsgenie/MSTeams/Slack/Webhook dispatchers — untouched; they gain a documented path (`ISystemEventDispatcher` + the same link) to follow later (§9).

## 5. Data / schema scope

New (migrations):

- **`OAuthToken` store** (§4.3): `{ IntegrationId (FK), AccessToken (encrypted), RefreshToken (encrypted), ExpiresAt, Scopes }`. Encrypted via `IDataProtector`. Naming/exact columns settled at implementation.
- **Per-provider OAuth-app config** for PagerDuty's `client_id`/`client_secret`/scopes/redirect URI — following the `OidcProviderConfig` precedent (DB row, admin-managed). May extend/parallel `OidcProviderConfig` rather than a wholly new entity; decided at implementation.
- **`ServiceIntegrationMapping`** (§4.5): `(ServiceId FK, IntegrationId FK)` composite PK, both `OnDelete Cascade`, plus a `MappingJson` (`jsonb`) holding provider-specific remote coordinates. This is both the N:M link and the mapping — one table, not two.
- **`Alert`** gains a nullable correlation-token column (`PagerDutyDedupKey` or generic `AlertEventCorrelationKey`) for `ResolveAsync` (§4.6).

Code-level (no migration): add `IntegrationCapability.SendsAlertEvents` (`1 << 7`, §4.8); flip PagerDuty's manifest flag; new DTOs `DiscoveredResourceDto` and the mapping create/delete request shapes (§4.4a); the two discovery/match endpoints on `IntegrationsController`; the `SystemEventHandler` on RFC 0009's engine (§4.6).

- **`PagerDutyConfig` loses its `RoutingKey` field.** With OAuth + discovery, each Piro Service maps to a different PagerDuty service, each with its own routing key — so the key lives per-mapping in `ServiceIntegrationMapping.MappingJson` (§4.5), never once at the Integration level. A manual `RoutingKey` on `PagerDutyConfig` would be a dead, confusing field that competes with the discovered key; it is removed. `PagerDutyConfig` ends up holding only OAuth-link metadata (or empty), not a key. (Since the field exists in code today but has no dispatcher consuming it, removing it has no behavioral migration cost.)

Explicitly **not** changed: `Integration`, `AlertConfig`, `Check`, `Service` existing columns, `EscalationPolicy`.

## 6. Phased plan

Single big-bang delivery for functional paging (per the §7 decision): there is no partial phase that pages via manually-entered Routing Keys. Phases below are implementation ordering within that delivery, not independently shippable paging milestones. **Phases 1–3 have no RFC-0009 dependency and can land before it; only phase 4 (dispatch wiring) requires RFC 0009's event+outbox (§4.6).**

1. **Generic OAuth framework** (§4.3): `IOAuthClient` (generalizing `OidcService`), the encrypted persistent token store, refresh-on-use (proactive background refresh + defensive lock). Validated in isolation against PagerDuty's authorize/token endpoints (connect + refresh round-trip) — no dispatch yet.
2. **PagerDuty discovery + match** (§4.4/§4.4a): the `GET …/discover` and `POST …/mappings` endpoints, live-not-cached discovery, the admin match UI. Routing keys stored per-mapping in `MappingJson`.
3. **Platform mapping + dispatcher** (§4.5, §4.7, §4.8): `ServiceIntegrationMapping`, `Alert` dedup column, `ISystemEventDispatcher`/`PagerDutyDispatcher` (with a manual/integration test firing a real trigger/resolve against a sandbox account), the `INotificationDispatcher` no-op, manifest flag + tests. The dispatcher works and is testable in isolation, but nothing calls it automatically yet.
4. **Dispatch wiring on RFC 0009's engine** (§4.6): the `SystemEventHandler` reacting to `AlertOpenedEvent`/`AlertResolvedEvent`, persisting/reading the `dedup_key`. **End of this phase = functional paging** (a check failing pages PagerDuty; recovery resolves it), verified against a real/sandbox PagerDuty account. **Gated on RFC 0009 landing.**
5. **(Out of scope, listed for direction)** Extend `ISystemEventDispatcher` to Opsgenie/MSTeams/Slack/Webhook (§9); validate the generic OAuth framework against a second consumer (GitHub or Jira, RFC 0012's Jira being the concrete one) to confirm the §2/§7 boundary holds.

## 7. Alternatives considered

- **Events-API-first phasing** (ship paging with manual Routing Keys, add OAuth later): rejected per the product decision that frictionless connect is the point of the feature. It would deliver value sooner, but the explicit goal is connect-once discovery, and a manual-key phase would ship a UX we intend to replace. Noted as the main tradeoff of the big-bang choice (§8).
- **Generic resource-discovery abstraction** (`IResourceDiscoverer` implemented by PagerDuty/GitHub/Jira): rejected — services+routing-keys, repos, and projects are too dissimilar to share a useful interface derived from one real consumer; it would be abstraction-by-guessing. The generic/specific line is drawn at the OAuth *flow* (§2, §4.3–4.4). Revisit once a second consumer exists.
- **REST-API-with-account-API-token instead of OAuth**: rejected — an account API token grants full-account access and is a far more sensitive secret than a scoped OAuth token with a refresh/revoke lifecycle; OAuth is PagerDuty's recommended path for new apps and matches the frictionless-connect goal.
- **Trigger events via OAuth/REST directly** (skip the routing key): impossible — no OAuth-authenticated trigger endpoint exists; Events API `enqueue` authenticates only via `routing_key` (§2, §4.6).
- **Single `Service.AlertIntegrationId` (1:1)** instead of the N:M mapping table: rejected — forecloses multi-channel fan-out without a later column→table migration (§4.5).
- **Typed per-provider columns (or a table per provider) for the mapping** instead of a shared `ServiceIntegrationMapping` with a `MappingJson` blob: rejected — the mapping stores identifiers of *remote* entities (routing keys, project keys, repo names), which have no Piro table to foreign-key to and a different shape per provider. Typed columns would force a schema migration for every new integration and fragment the concept across tables; the blob mirrors the `Integration.ConfigJson` pattern Piro already uses for exactly-this-kind of provider-specific data, with a typed C# class validating each provider's blob on deserialize (§4.5). Type-safety is recovered at the deserialize boundary, not the DB.
- **Two separate tables — one to link (N:M), one to map**: rejected — the link and the mapping are the same row (a link exists precisely to say "this Service corresponds to that remote entity"); splitting them adds a join and a consistency burden for no benefit (§4.5).
- **Fold shared-channel dispatch into `EscalationPolicy` steps**: rejected — duplicates PagerDuty's own escalation engine and forces a fire-once trigger into a step-timing model it doesn't fit (§3, §4.5).
- **Trigger from `EscalationCheckerService`**: rejected — that loop is per-user-handle; a shared channel has no place there, and it would delay PagerDuty until Piro's own escalation delays elapse, defeating the purpose (§4.6).
- **Reuse plaintext `ConfigJson` for refresh tokens**: rejected — refresh tokens are long-lived credentials; storing them in plaintext `jsonb` with no rotation is a security gap. Encrypted store via the already-registered `IDataProtector` instead (§4.3, §8).
- **Store the OAuth token in the in-memory `GcpTokenCache` style**: rejected — ephemeral per-process cache loses refresh tokens on restart, forcing re-consent; a persisted store is required for a connect-once experience.
- **Support `acknowledge`**: rejected for now — no Piro acknowledged-but-not-resolved state to fire it from (§2).

## 8. Risks

- **Big-bang scope + cross-RFC dependency.** This is realistically three features chained (OAuth framework + discovery + dispatcher), and the final dispatch wiring (§4.6) additionally depends on **RFC 0009** having landed (its `AlertOpenedEvent`/`AlertResolvedEvent` + outbox are what the `SystemEventHandler` hangs off). No functional paging lands until all of that does; the risk is the RFC becoming aspirational for its size. Mitigation: the §6 ordering makes phases 1–3 (OAuth framework, discovery, dispatcher-in-isolation) independent of 0009 and independently reviewable — real, testable artifacts land before the 0009-gated wiring; only phase 4 waits. If 0009 slips, phases 1–3 still deliver a connectable, discoverable, unit-tested dispatcher; if the team wants paging before 0009, the rejected Events-API-first phasing (§7) remains a fallback that wires the trigger directly instead of via the outbox.
- **Coordination with RFC 0009 and 0012 on shared enum bits and interface naming.** All three extend `IntegrationCapability` and touch the integration dispatch surface. This RFC takes capability bit `1 << 7` and names the interface `ISystemEventDispatcher` to match RFC 0009's references (§4.6, §4.8) — but if the RFCs land in a different order, the bit assignments (`1<<5` group, `1<<6` actions, `1<<7` alert-events) must be reconciled at merge time so no two claim the same bit. Whichever lands first fixes the lower bits; the rest rebase.
- **Refresh-token security.** The framework holds long-lived third-party credentials. They must be encrypted at rest (`IDataProtector`, §4.3) and never logged/returned (extend the `SecretField` masking discipline to the token store). A leak here is worse than a leaked static Routing Key — it can mint scoped access to the connected PagerDuty account until revoked.
- **Concurrent token refresh (addressed, with a residual edge).** Rotating refresh tokens make naive on-use refresh a guaranteed production bug (two concurrent dispatches refresh with the same token; the second gets `invalid_grant`). §4.3's proactive-background-refresh keeps the token fresh off the hot path so the race normally can't occur, and the defensive per-integration distributed lock covers the cold-start case. Residual edge to watch: if the background job is down *and* many dispatches hit a just-expired token simultaneously, they serialize on the lock (correct, but adds latency); monitor background-refresh health so the lock stays a rare fallback, not the primary path.
- **Generic-with-one-consumer.** Even scoped to the flow, "generic" is validated by a single real consumer (PagerDuty). GitHub/Jira are named checks (§2) but unimplemented; the abstraction may still need adjustment when the second consumer is built. Kept deliberately narrow (flow only, not discovery) to minimize this.
- **PagerDuty API specifics to verify at implementation.** Exact access-token lifetime (read `expires_in`, don't hard-code), the precise scope string for *provisioning* an integration, and current rate-limit numbers (REST ~960/min; Events ~480/min/key + dynamic) were Medium-confidence in research — confirm against live PagerDuty docs/app-registration before relying on any hard number.
- **Invalid/revoked authorization.** If the admin revokes Piro's OAuth app in PagerDuty, refresh fails; dispatch must degrade cleanly (log, surface in admin UI) rather than throw into alert processing. Same clean-failure posture as a bad routing key (§4.7).
- **Severity mapping drift.** Piro's severity model and PagerDuty's four-value enum may diverge over time; revisit the mapping explicitly rather than assume it stays 1:1.
- **`ISystemEventDispatcher` has one implementer initially.** A mild abstraction bet, justified because Opsgenie/MSTeams/Slack/Webhook are named existing stub candidates for the same interface (§9).

## 9. References

- [PagerDuty Events API v2 — Overview / enqueue](https://developer.pagerduty.com/docs/events-api-v2/overview/) — `routing_key`-only trigger auth; `event_action`/`severity` enums; `dedup_key`.
- [PagerDuty OAuth 2.0 functionality](https://developer.pagerduty.com/docs/f59fdbd94ceab-o-auth-functionality) & [Scoped OAuth / API scopes](https://www.pagerduty.com/blog/insights/build-sophisticated-apps-for-your-pagerduty-environment-using-oauth-2-0-and-api-scopes/) — authorization-code + PKCE, refresh tokens, `services.read`/`services.write`.
- [PagerDuty REST API authentication](https://developer.pagerduty.com/docs/ZG9jOjExMDI5NTUx-authentication) — REST v2 accepts OAuth bearer tokens interchangeably with API tokens.
- PagerDuty official `go-pagerduty` SDK (`service_integration.go`, `Integration.IntegrationKey`) and the [Terraform `pagerduty_service_integration`](https://registry.terraform.io/providers/PagerDuty/pagerduty/latest/docs/resources/service_integration) resource — confirm reading/provisioning `integration_key` via `GET`/`POST /services/{id}/integrations`.
- [PagerDuty REST](https://developer.pagerduty.com/docs/rest-api-rate-limits) & [Events](https://developer.pagerduty.com/docs/events-api-rate-limits) API rate limits.
- RFC 0001 (`docs/rfcs/0001-third-party-alert-ingestion.md`) §3 — "don't reinvent the source platform's alerting pipeline" (applied here in the sending direction) and §4.5/4.7 severity-mapping pattern.
- RFC 0003 (`docs/rfcs/0003-integration-manifest.md`) — `IntegrationManifestAttribute`/`IntegrationCapability`, extended here with `SendsAlertEvents`.
- RFC 0009 (`docs/rfcs/0009-system-notifications.md`) — the notification engine (`AlertOpenedEvent`/`AlertResolvedEvent` + durable outbox + handlers) this RFC's dispatch wiring (§4.6) consumes; 0009 §4.1 names this RFC's "mode 3" and draws the boundary. A prerequisite of phase 4 (§6, §8). Note: 0009 refers to this RFC's interface as `ISystemEventDispatcher` — this RFC adopts that name for consistency.
- RFC 0012 (`docs/rfcs/0012-integration-actions-with-dynamic-ui.md`) — the backend-classifies-frontend-places / config-as-schema convention this RFC's discovery+match UI (§4.4a) reuses; 0012 is the concrete GitHub/Jira-adjacent second consumer validating the generic OAuth framework (§2, §6). 0012 claims capability bit `1 << 6` (`ProvidesActions`); this RFC coordinates around it (§4.8, §8).

## Out-of-scope follow-up (recommended issue)

Opsgenie and MSTeams (`OpsgenieDispatcher.cs`, `MsTeamsDispatcher.cs`) are registered `INotificationDispatcher`s whose manifests already declare `SendsPersonalNotification` while both methods are `Task.FromResult(false)` stubs — a manifest inconsistency predating this RFC, arguably worse than PagerDuty's honest `IntegrationCapability.None` because an admin would reasonably believe they work. Recommend a separate issue to either fix the manifest to `None` until real, or implement `ISystemEventDispatcher` for both once this RFC's interface exists (reusing the same link, trigger point, and manifest/test machinery — the §6 phase-4 direction).
