# RFC 0007 — Service impact analysis (blast radius & propagation reasons)

Status: proposal
Author: Arael Espinosa (assisted draft)
Date: 2026-07-17

## 1. Problem

Piro already models dependencies between services and already propagates status
through them. `ServiceDependency` (`src/Piro.Domain/Entities/ServiceDependency.cs:11`)
is a directed edge in a status-propagation DAG with three modes — `Blocking`,
`SoftBlocking`, `Advisory` (`src/Piro.Domain/Enums/DependencyPropagationMode.cs:4`) —
and `ServiceStatusService.ComputeAsync` walks the upstream edges on every recompute,
worsening a service's derived `CurrentStatus` when a dependency it relies on is `DOWN`
or `DEGRADED` (`src/Piro.Application/Services/ServiceStatusService.cs:54-70`). Cycle
detection, cascade recomputation, and a full CRUD API
(`src/Piro.Api/Controllers/DependenciesController.cs`) all exist.

The mechanism works. What's missing is the ability to **read impact as a first-class
thing**. Three concrete failure modes follow:

1. **You can't answer "what breaks if this goes down" without breaking it.** The blast
   radius of a service is implicit in the graph, computed only *reactively* as statuses
   change. There is no way to ask, ahead of an outage or a maintenance window, "if
   `postgres-primary` goes `DOWN`, which downstream services degrade, and how badly?"
   Operators planning maintenance or triaging an incident have no preview.

2. **"Why is this service degraded?" has no answer surface.** `ComputeAsync` knows
   exactly which upstream services dragged a service down — it builds a
   `propagationSources` list of their slugs (`ServiceStatusService.cs:55,69`). That list
   is then passed to `PersistAndCascadeAsync` and **silently discarded** — the parameter
   is never read or stored (`ServiceStatusService.cs:121-138`). A service shows
   `DEGRADED` on the admin panel with no indication that the cause is an upstream
   dependency rather than its own checks. [Issue #19](https://github.com/Heva-Co/piro/issues/19)
   ("Propagation chain page: show why a service is degraded") asks for exactly this and
   is blocked because the backend throws the reason away.

3. **Incidents don't know the dependency graph exists.** When an admin links an alert to
   an incident, the incident's affected-services list is seeded only from the alert's own
   `ServiceId` (`AlertAppService.LinkToIncidentAsync`,
   `src/Piro.Application/Services/AlertAppService.cs:80-104`). If `postgres-primary` has
   an outage that (via `Blocking` edges) is already degrading five downstream services,
   the human declaring the incident has to know and add all five by hand. The graph that
   already computed that impact contributes nothing to the incident.

This RFC does not add dependencies — it makes the impact that dependencies already
produce **queryable, explainable, and reusable**.

## 2. Non-goals

- **A new dependency model, new edge types, or hierarchy/grouping of services.** The DAG
  and its three propagation modes stay exactly as they are. There is deliberately no
  `ServiceGroup`/`Component` concept — services remain flat, related only through
  `ServiceDependency` edges. Introducing a hierarchy is a separate proposal.
- **Persisted status history / time-series of derived status.** A `ServiceStatusSnapshots`
  table (with a `PropagationSources` column) once existed and was deliberately dropped
  (`src/Piro.Infrastructure/Migrations/20260703172728_DropServiceStatusSnapshots.cs`).
  This RFC persists the *current* propagation reason on the live entity, not a historical
  record. Reconstructing "why was X degraded at 3pm last Tuesday" is out of scope and
  would need its own RFC that re-litigates that deletion.
- **Auto-opening incidents from dependency propagation.** Promoting a signal to an
  incident stays a manual admin decision, consistent with the internal-alert flow and
  with [RFC 0001 §3](0001-third-party-alert-ingestion.md). This RFC *offers* the
  dependency-derived affected services to a human who is already declaring an incident;
  it never declares one on its own.
- **The graph visualization UI.** [Issue #16](https://github.com/Heva-Co/piro/issues/16)
  (D3 force-directed graph) and [issue #2](https://github.com/Heva-Co/piro/issues/2)
  (dependency graph management) are frontend work that consumes the read API this RFC
  adds. The rendering is not designed here.
- **Changing how `PublicStatus` behaves.** Raw check failures and dependency propagation
  affect `CurrentStatus` (internal) only; `PublicStatus` is still moved only by
  maintenance or a Public incident's declared impact
  (`ServiceStatusService.cs:32-36`). Blast radius is an internal/operator concern.

## 3. Design principle

**Read the graph, don't rebuild it.** Every impact question this RFC answers is already
implicit in `ServiceDependency` edges and the `Worst`/propagation logic in
`ComputeAsync`. Blast radius is a *forward walk* of the same edges the reactive
propagation walks backward; the propagation reason is a value `ComputeAsync` already
computes and discards. So the design is overwhelmingly about **surfacing existing state
and reusing existing traversal logic** — one new persisted field, one read-only
traversal service, and one optional hook into incident creation. No new pipeline, no
duplicated propagation rules.

## 4. Design

### 4.1 Flow

```
                      ┌─────────────────────────────────────────────┐
   (a) blast radius   │  GET /api/v1/services/{slug}/impact          │
   preview  ──────────▶│  ImpactAnalysisService.GetBlastRadiusAsync   │
   (read-only)        │    forward BFS over DependedOnBy edges        │
                      │    simulate: "if this service = DOWN, who?"   │
                      └─────────────────────────────────────────────┘
                                        │ reuses
                                        ▼
              ServiceDependencyRepository.GetBlockingDownstreamServiceIdsAsync
                        (same edges the reactive cascade uses)

   (b) propagation      ComputeAsync builds propagationSources  ──┐  (already computed today)
   reason               (ServiceStatusService.cs:55,69)           │
   (persist, not        PersistAndCascadeAsync now WRITES it ─────┘
    discard)                       ↓
                         Service.PropagationReason (new column)
                                    ↓
                         GET /public|admin service DTO exposes it
                         → answers issue #19 "why is X degraded"

   (c) incident         AlertAppService.LinkToIncidentAsync
   seeding                 (optional) IncludeDownstream flag
   (offer, not             → ImpactAnalysisService gives current
    impose)                   downstream-degraded services
                            → pre-populates IncidentService rows,
                              human confirms before publish
```

### 4.2 `ImpactAnalysisService` — a read-only forward traversal

A new application service, `ImpactAnalysisService`, answering two questions without
mutating anything:

- **`GetBlastRadiusAsync(serviceSlug, ct)`** — the *hypothetical* forward walk. Starting
  from the target service, follow `DependedOnBy` edges (downstream) and compute, for each
  reachable service, the worst status it *would* take if the target went `DOWN` right
  now — applying the same `Blocking`-propagates-exactly / `SoftBlocking`-caps-at-`DEGRADED`
  / `Advisory`-ignored rules already encoded in `ComputeAsync`
  (`ServiceStatusService.cs:64-66`). Returns each affected service with the hop distance
  and the projected `ServiceStatus`. `Advisory` edges are excluded from the projection
  (they carry no status effect) but *can* optionally be reported as "informational
  neighbors."

- **`GetDependentsAsync(serviceSlug, ct)`** — the *current* factual walk: which
  downstream services are degraded *right now* and have this service in their propagation
  chain. This is what incident seeding (§4.4) consumes.

The traversal reuses `IServiceDependencyRepository.GetBlockingDownstreamServiceIdsAsync`
(`src/Piro.Infrastructure/Persistence/Repositories/ServiceDependencyRepository.cs:53-58`)
— the exact query the reactive cascade uses to find who to recompute
(`ServiceStatusService.cs:136`). The forward BFS mirrors, in the opposite direction, the
acyclicity BFS already written in `DependencyService.ValidateAcyclicAsync`
(`src/Piro.Application/Services/DependencyService.cs:85-107`); because the graph is a
guaranteed DAG (`CyclicDependencyException` blocks cycles at write time), the walk
terminates without a visited-set safety net being load-bearing — though we keep one, as
that BFS does.

The propagation projection is severity-monotonic: a service's projected status is the
`Worst` of the projections arriving on all its inbound edges, so a diamond dependency
(two paths to the same downstream service) resolves deterministically regardless of BFS
order.

Registered in `Program.cs` alongside the other AppServices (`ServiceAppService` at
`src/Piro.Api/Program.cs:179`).

### 4.3 Persist the propagation reason instead of discarding it

`ComputeAsync` already collects `propagationSources` — the slugs of the upstream services
that worsened this service's status (`ServiceStatusService.cs:55,69`). Today that list
dies at the `PersistAndCascadeAsync` boundary. This RFC stores it.

Add a single nullable column to `Service`:

- **`Service.PropagationReason` (`string?`)** — a compact, denormalized record of *why*
  the current `CurrentStatus` is what it is when the cause is upstream: the slugs (and
  their contributing status) that dragged this service down on the last recompute. Null
  when the status comes from the service's own checks, maintenance, or a direct incident
  — i.e. null means "not caused by a dependency."

`PersistAndCascadeAsync` (`ServiceStatusService.cs:121`) already receives
`propagationSources`; it will now write it to this column (empty list → null) in the same
update it already does for `CurrentStatus`/`PublicStatus`. No new write path, no extra
save — one more assigned property on an entity that is already being persisted in that
method.

This is intentionally a live, overwrite-on-each-recompute field on the entity, **not** a
revival of the dropped `ServiceStatusSnapshots` history (§2). It answers "why is this
degraded *now*," which is what [issue #19](https://github.com/Heva-Co/piro/issues/19)
asks for.

### 4.4 Optional dependency-aware incident seeding

`AlertAppService.LinkToIncidentAsync` currently seeds an incident's `IncidentService`
rows from the alert's own service only (`AlertAppService.cs:80-104`). This RFC adds an
opt-in path: when the linking request sets a new flag (see below), the service calls
`ImpactAnalysisService.GetDependentsAsync` on the alert's service and pre-populates the
downstream services that are *currently* degraded because of it, each with its
`IncidentService.Impact` set to the projected status and `TriggeringCheckId` left null
(matching how manually-added affected services already look —
`src/Piro.Domain/Entities/IncidentService.cs:15`).

- The flag lives on the existing `LinkAlertToIncidentRequest`
  (`src/Piro.Application/DTOs/AlertSummaryDto.cs:101`) as an optional
  `bool IncludeDownstreamServices` (default `false`) — the current behavior is unchanged
  unless a human opts in.
- It **offers**, it does not impose: the pre-populated rows are just `IncidentService`
  entries the admin can remove before the incident is published, exactly as if they'd
  added them manually. Consistent with "never link automatically" (`AlertAppService.cs:48-52`).

This reuses the incident/affected-services model wholesale — `IncidentService` with its
per-service `Impact` (`IncidentService.cs:12`) already exists and already feeds
`ServiceStatusService` via `GetActiveImpactForServiceAsync`
(`ServiceStatusService.cs:30`). No new incident structure.

### 4.5 What does NOT change

- **`ServiceDependency`, `DependencyPropagationMode`, `DependencyService`, and the
  `DependenciesController` CRUD API** — untouched. Edges are created/removed exactly as
  today; cycle detection and cascade-on-mutation (`DependencyService.cs:61,78`) are
  unchanged.
- **The reactive propagation in `ComputeAsync`** — the backward walk and the
  `Blocking`/`SoftBlocking`/`Advisory` rules are the single source of truth for status.
  §4.2's forward walk *reads the same edges and reuses the same rules*; it does not fork
  the propagation logic. If the modes' semantics ever change, both directions change in
  one place.
- **The single-writer status pipeline.** `ComputeAsync`/`ComputeAllWithCascadeAsync`
  remain callable only through the `Channel<CheckStatusChangedEvent>` drained by
  `StatusDrainHostedService` (`ServiceStatusService.cs:91-98`). `ImpactAnalysisService` is
  strictly read-only and never writes status, so it introduces no new race against that
  serialized writer.
- **`PublicStatus` and the public status page.** Blast radius and propagation reason are
  operator-facing (`CurrentStatus` side). `PublicController`
  (`src/Piro.Api/Controllers/PublicController.cs`) and `PublicStatus` behavior are
  unchanged.
- **Alert firing/evaluation.** `AlertEvaluationService`/`AlertLifecycleService` are not
  touched; §4.4 only extends what happens *after* a human chooses to link an alert to an
  incident.

## 5. Data / schema scope

- **One new column**: `Service.PropagationReason` (`text`, nullable). Added in
  `ServiceConfiguration` (`src/Piro.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs:9`)
  and a new EF Core migration.
- **One new optional DTO field**: `LinkAlertToIncidentRequest.IncludeDownstreamServices`
  (`bool`, default `false`) in `src/Piro.Application/DTOs/AlertSummaryDto.cs`. Non-breaking
  — omitting it preserves today's behavior.
- **New read DTOs** for the impact endpoint (e.g. `BlastRadiusDto`,
  `ImpactedServiceDto { Slug, Name, HopDistance, ProjectedStatus }`), mapped via a
  `ToDto` extension following the established pattern
  (`src/Piro.Application/Extensions/DependencyExtensions.cs:9`).
- **No changes to**: `ServiceDependency`, `DependencyPropagationMode`, `Incident`,
  `IncidentService`, `IncidentImpactChange`, `Alert`, `AlertConfig`, `Check`, or the
  `ServiceStatus` enum. No table added or dropped. The dropped `ServiceStatusSnapshots`
  table is **not** revived.
- **Admin API types**: after the DTOs land, regenerate `apps/admin/src/lib/api-types.ts`
  via `pnpm run generate:api-types` (per AGENTS.md) so the graph/impact UI issues (#16,
  #19, #2) consume typed responses.

## 6. Phased plan

Each phase is independently shippable and independently useful.

1. **Persist the propagation reason.** Add `Service.PropagationReason`, write it in
   `PersistAndCascadeAsync` from the `propagationSources` already flowing in, expose it on
   the admin service DTO. This alone unblocks [issue #19](https://github.com/Heva-Co/piro/issues/19)
   ("why is X degraded") with the smallest possible change — no new service, no new
   endpoint, one column and one already-available list.

2. **`ImpactAnalysisService` + read endpoint.** Add the forward-traversal service and
   `GET /api/v1/services/{slug}/impact` returning the blast-radius projection. This is the
   backend the graph-visualization issues (#16, #2) need and provides the pre-outage
   "what breaks if this goes down" preview. Read-only, no schema change beyond phase 1's.

3. **Dependency-aware incident seeding.** Add `IncludeDownstreamServices` to the link
   request and wire `AlertAppService.LinkToIncidentAsync` to pre-populate downstream
   affected services from `ImpactAnalysisService.GetDependentsAsync`. This is last on
   purpose: it changes incident-declaration UX and benefits from validating phases 1–2 in
   real triage first, and its "offer, human confirms" contract deserves its own review.

## 7. Alternatives considered

- **Revive `ServiceStatusSnapshots` to store impact history.** Rejected — the table was
  deliberately dropped (§2), and the immediate need (issues #19/#16) is about *current*
  impact, not historical reconstruction. Persisting one live `PropagationReason` field
  meets that need without re-introducing a table someone chose to remove; a history
  feature can make its own case separately.

- **Compute blast radius by dry-running `ComputeAsync` against a simulated status.**
  Rejected — `ComputeAsync` is a mutating, single-writer method guarded behind the status
  channel (`ServiceStatusService.cs:81-101`); calling it (or a copy of it) to answer a
  read-only question would either mutate real status or require carefully neutering the
  persist path. A dedicated read-only forward walk that reuses the same *edges and rules*
  is safer and can't race the writer.

- **Auto-populate incident affected services unconditionally (no flag).** Rejected — it
  breaks the "Piro never links/decides automatically" contract
  (`AlertAppService.cs:48-52`) and would surprise admins by attaching services they
  didn't choose. An opt-in flag keeps today's behavior the default.

- **Store the propagation reason as a structured child table (one row per source) instead
  of a denormalized string.** Rejected for now — the reason is a small, always-read-as-a-
  whole, overwrite-on-recompute value with no need to be queried by source. A normalized
  table adds write amplification on the hot recompute path for no current query benefit.
  If per-source querying becomes a real need, promoting the column is a contained follow-up.

## 8. Risks

- **Stale `PropagationReason` after the cause clears.** The field is only rewritten when
  `ComputeAsync` runs for *that* service. Recompute already cascades to blocking
  downstream services when an upstream status changes
  (`PersistAndCascadeAsync` → `GetBlockingDownstreamServiceIdsAsync`,
  `ServiceStatusService.cs:136`), so in normal operation the reason is refreshed whenever
  the status that produced it changes. The edge to watch: `SoftBlocking`/`Advisory` edges
  are excluded from `GetBlockingDownstreamServiceIdsAsync`'s cascade set — verify that a
  service degraded via a `SoftBlocking` edge still gets its `PropagationReason` cleared
  when the upstream recovers, and if not, that its next own-check recompute overwrites it.

- **Blast-radius projection can diverge from reactive reality.** The forward walk assumes
  the target goes fully `DOWN`; the reactive propagation reacts to whatever the real
  upstream status is (`DOWN` vs `DEGRADED`). The endpoint must be labeled as a
  *worst-case hypothetical* ("if this service went down"), not a live status mirror, or
  operators will read a scary projection as the current state.

- **Large fan-out graphs.** A widely-depended-on service (e.g. a shared database) could
  have a large downstream set; the forward BFS is O(edges) per call. It's bounded by the
  DAG and runs on an admin-triggered read, not the hot check path, so this is low-risk —
  but the endpoint should not be called on every status poll of a dashboard. Callers
  should treat it as an on-demand analysis, not a per-render lookup.

- **Incident seeding drift (phase 3).** `GetDependentsAsync` reflects the graph at link
  time; if edges change between linking and publishing, the seeded affected-services list
  can be slightly stale. Because it's an *offer* the human confirms before publish, this
  is acceptable — but the UI should make clear the list is a snapshot suggestion, not a
  live-maintained set.
