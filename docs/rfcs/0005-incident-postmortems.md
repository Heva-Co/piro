---
rfc: 5
title: "Postmortems (standalone post-incident review reports)"
status: implemented
created: 2026-07-16
proposal-pr: 176
depends-on: []
implementation-pr: 205
---

# RFC 0005 — Postmortems (standalone post-incident review reports)

Status: proposal
Author: Arael Espinosa (assisted draft)
Date: 2026-07-16

## 1. Problem

When incidents are resolved in Piro, the machine-recorded history survives but the *analysis* of what happened does not. An incident already carries a rich, structured record — a status ladder (`IncidentStatus { Investigating, Identified, Monitoring, Resolved, Merged }`, `src/Piro.Domain/Enums/IncidentStatus.cs:4`), a chronological typed event log (`IncidentTimelineEvent`, `src/Piro.Domain/Entities/IncidentTimelineEvent.cs`), an impact-severity history (`IncidentImpactChange`, `src/Piro.Domain/Entities/IncidentImpactChange.cs`), the alerts that fired it (`Incident.Alerts`, `src/Piro.Domain/Entities/Incident.cs:50`), and the services it affected (`Incident.IncidentServices`, `Incident.cs:48`). But **there is nowhere to record the human review that turns that raw history into learning** — root cause, contributing factors, what went well/badly, and action items with an accountable owner. Piro has no postmortem concept at all today: no entity, no enum, no DTO, no column, not even a half-implemented field. This is greenfield.

The concrete failure modes:

1. **The learning is lost.** Once an incident hits `Resolved`, the only durable artifact is the operational timeline of what happened. There is no structured home for the post-incident review — root cause, impact analysis, action items — so teams either skip it or write it in an external doc that drifts away from Piro.
2. **No owner of the review process.** `Incident.AcknowledgedBy` (`Incident.cs:30`) records who acknowledged the fire, but nothing records who *owns running the review* and driving action items to done — the single most important accountability field in any postmortem process (PagerDuty names it "Owner of Review Process").
3. **No draft→publish lifecycle for the analysis.** A postmortem is drafted, reviewed in a meeting, then finalized. Piro has this exact lifecycle shape for incident *visibility* (`IncidentVisibility { Private, Public }`, `PublishAsync`/`UnpublishAsync` at `IncidentAppService.cs:325,349`) but nothing analogous for a review document.
4. **A single review may span more than one incident.** A cascading outage can open several correlated incidents; the retrospective is one review covering all of them. Nothing today can group multiple incidents under one analysis.

This RFC is the design behind the already-open issue **[#63 "Postmortem / RCA attached to resolved incidents"](https://github.com/Heva-Co/piro/issues/63)** (labels `enhancement`, `backend`, `frontend`). Issue #63 asks for root cause, timeline of events, impact summary, and action items "stored against the incident and optionally visible on the public status page." This RFC broadens the anchoring (§3): the postmortem is a **standalone report** with its own name and impact window that **references one or more incidents**, closely mirroring PagerDuty's model, and public-page visibility is deferred to a later phase (§2, §6).

### Relationship to PagerDuty's postmortem model

The reference process (https://response.pagerduty.com/after/post_mortem_process/) models a postmortem as a **standalone report**: it has its own name, an owner of the review process, an impact start/end time, a draft/published status, "data sources" (the incidents that fall inside the impact window), a hand-assembled timeline, and a fixed set of analysis sections (Overview, What Happened, Resolution, Root Causes, Impact, What Went Well, What Didn't Go So Well, Action Items). This RFC deliberately follows that shape — a free-standing report referencing N incidents — because it matches how teams actually run reviews (one meeting, one document, possibly several correlated incidents) and it is what issue #63's reporter is asking for.

Where Piro *improves* on PagerDuty: PagerDuty makes the author hand-assemble the timeline from data sources. Piro's incidents already own their timelines (`IncidentTimelineEvent`), impact history (`IncidentImpactChange`), and firing alerts (`Alert`) — so the report's timeline is **derived** by merging the referenced incidents' existing events, and the author only *adds* the annotations the machine couldn't record (§4.4).

## 2. Non-goals

- **Public status-page visibility of postmortems.** Issue #63 mentions "optionally visible on the public status page." Publishing to the public page (`apps/web`) introduces redaction of internal root-cause detail and a public-vs-internal content split; it is deferred to Phase 3 (§6). Phases 1–2 keep postmortems internal-only, behind the same admin authorization as the rest of the API.
- **Reimplementing the incident timeline.** `IncidentTimelineEvent` already is a per-incident, chronological, typed event log. The report's timeline **merges** the referenced incidents' existing events with the report's own manual annotations (§4.4) — it does not copy or re-collect incident events into its own store. Manual annotations live in the report (`PostmortemTimelineEntry`, §4.4); machine events stay owned by the incident.
- **Rich-text / Markdown, file attachments, embedded images.** Section and annotation bodies are plain text (`string`) in Phase 1. A Markdown/attachment story is a later additive enhancement.
- **Auto-generating analysis prose (LLM/summarization).** Out of scope. The analysis is authored by humans; Piro only pre-populates the *structured* data it already owns (the referenced incidents' timelines, alerts, impact windows).
- **Config-as-code (YAML) import/export of postmortems.** Piro has an active config-as-code track (issues #23–#29), but postmortems are point-in-time review artifacts, not declarative configuration. Not in scope.
- **Multi-incident *merge* semantics.** Referencing several incidents in one report (§4.6) is not the same as `IncidentStatus.Merged` (`IncidentStatus.cs:13`), which is an incident-domain concept. A postmortem referencing N incidents leaves those incidents entirely untouched (§4.8).

## 3. Design principle

**A postmortem is a standalone review report that *references* incidents and *derives* its factual timeline from them, adding only the human judgement the machine can't produce.** Three consequences shape every choice in §4:

1. **Free-standing report, N incidents.** The `Postmortem` is its own aggregate with its own name, owner, impact window, and draft/publish status. It references incidents through a join table (`PostmortemIncident`, §4.6), N:M — one report may cover several correlated incidents, and (in principle) an incident could be cited by more than one report. This matches PagerDuty and issue #4's reporter, and is why the API is a top-level `/api/v1/postmortems` resource (§4.5), not a sub-resource of incidents.

2. **Derive the factual timeline, own only the annotations.** The report's timeline is the chronological *merge* of (a) the referenced incidents' existing `IncidentTimelineEvent` / `IncidentImpactChange` / `Alert` events — read, never copied — and (b) the report's own `PostmortemTimelineEntry` rows, which hold the manual annotations an author adds ("vendor confirmed the outage at 14:32"). The author can only add/edit/delete their own annotation entries; the incident-derived events are read-only in the report (§4.4).

3. **The analysis sections are a definition table + a value table, not schema.** PagerDuty's report has eight fixed sections. Modeling each as a column would make "add a section" a migration, and hard-coding them as an enum would prevent ever customizing them. Instead there are two tables: **`PostmortemFieldDefinition`** describes *what sections exist* (heading, help text, type, order), and **`PostmortemFieldValue`** holds *the text a given report put in each section*. The eight standard sections are seeded field definitions; a report is created with one empty value row per active definition. In this RFC the definition set is a **fixed, non-user-editable global template** — but because it lives in a table rather than in an enum or in columns, a future phase can let admins add custom fields with zero schema change (§4.3). This separation of definition from value mirrors how the rest of the domain keeps heterogeneous, extensible data as rows rather than columns-per-type.

## 4. Design

### 4.1 End-to-end flow

Steps marked **[new]** don't exist today. Everything under "derive" is read through existing `Incident` relationships.

```
CREATE  [new — §4.2, §4.5]
  A Member/Admin/Owner creates a report
        POST /api/v1/postmortems   { name, reviewOwnerUserId?, impactStartAt?, impactEndAt? }
                    ↓
  PostmortemAppService.CreateAsync(request)
    - Status = Draft
    - resolves reviewOwnerUserId → AppUser; snapshots ReviewOwnerName (§4.2 / §7)
    - inserts one empty PostmortemFieldValue per active field definition — §4.3
                    ↓
  Postmortem row + one field-value row per active PostmortemFieldDefinition persisted

LINK INCIDENTS  [new — §4.6]
        POST   /api/v1/postmortems/{id}/incidents   { incidentId }
        DELETE /api/v1/postmortems/{id}/incidents/{incidentId}
                    ↓
  PostmortemIncident join rows (N:M). Suggested set = incidents whose window
  overlaps [ImpactStartAt, ImpactEndAt], but the author chooses explicitly.

EDIT  [new — §4.5]
  Author fills section bodies, adds/removes custom sections, adds timeline annotations
        PUT    /api/v1/postmortems/{id}
        POST   /api/v1/postmortems/{id}/timeline        { occurredAt, body }   → PostmortemTimelineEntry
        PUT    /api/v1/postmortems/{id}/timeline/{entryId}
        DELETE /api/v1/postmortems/{id}/timeline/{entryId}

READ  [new — §4.4, §4.5]
        GET /api/v1/postmortems/{id}
  Returns: the Postmortem + sections + the MERGED timeline:
    (referenced incidents' TimelineEvents / ImpactChanges / Alerts)  ← read-only, derived
    ∪ (this report's PostmortemTimelineEntry rows)                   ← author-owned
  sorted chronologically.

PUBLISH  [new — §4.5, mirrors IncidentAppService.PublishAsync]
        POST /api/v1/postmortems/{id}/publish   (and /unpublish)
                    ↓
  Status Draft → Published, PublishedAt stamped.
```

No background job, no dispatcher, no external I/O — a pure CRUD feature over a new aggregate that reads existing incident data. That is deliberate (§3).

### 4.2 `Postmortem` entity — the report aggregate

New entity `src/Piro.Domain/Entities/Postmortem.cs`, following Piro's modern-entity conventions (`int` Id, no base class, `DateTimeOffset` for event timestamps, `DateTime CreatedAt/UpdatedAt` audit fields set centrally by `PiroDbContext.SaveChangesAsync`, `PiroDbContext.cs:62`):

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `int` | no | matches the `int` Id convention (`Incident.cs:8`) |
| `Name` | `string` | no | the report name (PagerDuty "Report Name") |
| `Status` | `PostmortemStatus` | no | `Draft` (default) / `Published` — §4.3 |
| `ReviewOwnerUserId` | `int?` | yes | **FK to `AppUser`**, `ON DELETE SET NULL` (§4.7). First user FK in the incident-adjacent domain — see §7. |
| `ReviewOwnerName` | `string?` | yes | **denormalized snapshot** of the owner's display name at assign time. Preserves attribution even if the `AppUser` is deleted and the FK nulls out (§7). |
| `ImpactStartAt` | `DateTimeOffset?` | yes | optional impact window start (PagerDuty "Impact Start Time") |
| `ImpactEndAt` | `DateTimeOffset?` | yes | optional impact window end |
| `PublishedAt` | `DateTimeOffset?` | yes | stamped on publish, mirrors the publish-lifecycle precedent |
| `CreatedAt` | `DateTime` | no | audit — set centrally (`PiroDbContext.cs:62`) |
| `UpdatedAt` | `DateTime` | no | audit — set centrally |
| `FieldValues` | `ICollection<PostmortemFieldValue>` | — | nav — analysis content, one per active field definition (§4.3) |
| `TimelineEntries` | `ICollection<PostmortemTimelineEntry>` | — | nav — author annotations (§4.4) |
| `PostmortemIncidents` | `ICollection<PostmortemIncident>` | — | nav — referenced incidents, N:M (§4.6) |
| `ReviewOwner` | `AppUser?` | — | nav to the owner (may be null after owner deletion) |

The dual `ReviewOwnerUserId` + `ReviewOwnerName` is intentional and is the resolution of the "FK vs. preserve-on-delete" tension (§7): the FK gives a live, correct link while the user exists; the snapshot string guarantees the historical record still names who owned the review after the user is gone.

### 4.3 Analysis fields: a definition table + a value table

The eight analysis sections are modeled as **field definitions** (what sections exist) separated from **field values** (what a given report wrote in them). This is principle 3 (§3): the definition set is a fixed global template in Phase 1, but living in a table rather than an enum or columns means custom fields become possible later with no migration.

**`PostmortemFieldDefinition`** — `src/Piro.Domain/Entities/PostmortemFieldDefinition.cs`. The template. Seeded with the eight standard sections; **not user-editable in this RFC** (Phase 1).

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `int` | no | |
| `Key` | `string` | no | stable identifier, unique (e.g. `overview`, `root_causes`) — survives heading renames |
| `Heading` | `string` | no | display heading (e.g. "Root Causes") |
| `HelpText` | `string?` | yes | the guidance blurb PagerDuty shows under each heading |
| `FieldType` | `PostmortemFieldType` | no | `Text`, `LongText`, `Date`, `Select` (extensible — see below) |
| `SortOrder` | `int` | no | display order; standard sections seeded 0–7 |
| `IsActive` | `bool` | no | soft-disable a field without deleting historical values; default `true` |
| `IsSystem` | `bool` | no | `true` for the eight seeded fields — marks them as non-deletable, and reserves the door for user-defined (`false`) fields in a later phase |

**`PostmortemFieldValue`** — `src/Piro.Domain/Entities/PostmortemFieldValue.cs`. One row per report per active field definition.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `int` | no | |
| `PostmortemId` | `int` | no | FK to `Postmortem`, cascade delete |
| `FieldDefinitionId` | `int` | no | FK to `PostmortemFieldDefinition`, `RESTRICT` (a definition in use can't be hard-deleted — deactivate via `IsActive` instead) |
| `Value` | `string` | no | the authored content, defaults `""` (Phase 1 — plain text, no Markdown, §2) |

On report creation, `PostmortemAppService` inserts one empty `PostmortemFieldValue` for each `IsActive` definition (§4.5). The UI renders each value against its definition's `Heading`/`HelpText`/`FieldType`, ordered by the definition's `SortOrder`.

**`PostmortemFieldType`** — new enum `src/Piro.Domain/Enums/PostmortemFieldType.cs`, persisted as string via `.HasConversion<string>()` (the established pattern, `IncidentConfiguration.cs:18`): `Text`, `LongText`, `Date`, `Select`. The eight standard sections are all `LongText`. The enum exists from the start so the definition table can declare a field's shape; richer per-type validation and input widgets beyond a textarea are Phase 2+ (§6).

**Seed data** — the eight `IsSystem` definitions, matching PagerDuty's sections and help text:

```
Key             Heading                 (SortOrder, FieldType)
overview        Overview                (0, LongText)
what_happened   What Happened           (1, LongText)
resolution      Resolution              (2, LongText)
root_causes     Root Causes             (3, LongText)
impact          Impact                  (4, LongText)
what_went_well  What Went Well?         (5, LongText)
what_didnt      What Didn't Go So Well? (6, LongText)
action_items    Action Items            (7, LongText)
```

Seeded via EF `HasData` in `PostmortemFieldDefinitionConfiguration`, so the template ships with the migration and is present on every instance.

**`PostmortemStatus`** — new enum `src/Piro.Domain/Enums/PostmortemStatus.cs` (`Draft`, `Published`), same string persistence, modeled on the `IncidentVisibility`/publish-lifecycle precedent (`IncidentVisibility.cs:4`, `IncidentAppService.PublishAsync` at `:325`).

New enum `src/Piro.Domain/Enums/PostmortemStatus.cs` (`Draft`, `Published`), same string persistence, modeled on the `IncidentVisibility`/publish-lifecycle precedent (`IncidentVisibility.cs:4`, `IncidentAppService.PublishAsync` at `:325`).

### 4.4 Timeline: derived from incidents, annotations owned by the report

The report's timeline is a read-time **merge** of two sources, sorted chronologically:

**(a) Derived, read-only — from each referenced incident** (read through the repository's `Include`/`AsSplitQuery` pattern, matching `IncidentRepository.cs:43`):
- `Incident.TimelineEvents` (`IncidentTimelineEvent`, `Incident.cs:47`) — `OccurredAt`, `Type` (`TimelineEventType`), `ActorName`, `Comment`, status transitions.
- `Incident.ImpactChanges` (`IncidentImpactChange`, `Incident.cs:49`) — impact-severity-over-time.
- `Incident.Alerts` (`Incident.cs:50`) — firing alerts with `FiredAt`/`ResolvedAt` (`Alert.cs:43,46`).

These are never copied into the postmortem; they are projected into a read-only view. Editing an incident later changes what the report shows retroactively — intentional single-source-of-truth behavior, with the caveat noted in §8.

**(b) Author-owned — the report's own annotations:** new entity `src/Piro.Domain/Entities/PostmortemTimelineEntry.cs`:

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `int` | no | |
| `PostmortemId` | `int` | no | FK to parent, cascade delete |
| `OccurredAt` | `DateTimeOffset` | no | when the annotated event happened (so it sorts into the merge) |
| `Body` | `string` | no | the annotation text |
| `AuthorName` | `string?` | yes | denormalized author display name, mirroring `IncidentTimelineEvent.ActorName` (`:21`) |
| `CreatedAt` / `UpdatedAt` | `DateTime` | no | audit |

The author adds/edits/deletes only these entries; the derived (a) events are read-only within the report. Annotations live in the postmortem — they are not written back onto the incident.

### 4.5 Application + API layer

Following Piro's established CRUD pattern — concrete App service (no interface), per-entity repository, `record` DTOs, and a **new top-level controller** (the report is a free-standing resource, §3):

- **Repository** — `IPostmortemRepository` (`src/Piro.Application/Interfaces/IPostmortemRepository.cs`) + `PostmortemRepository` (`src/Piro.Infrastructure/Persistence/Repositories/PostmortemRepository.cs`), same shape as `IIncidentRepository`/`IncidentRepository` (`IncidentRepository.cs:9`), with `Include` of sections, timeline entries, and referenced incidents' timelines. Registered `services.AddScoped<IPostmortemRepository, PostmortemRepository>();` in `InfrastructureServiceExtensions.cs` (alongside `:86-91`).
- **App service** — `PostmortemAppService` (`src/Piro.Application/Services/PostmortemAppService.cs`), concrete class with primary-constructor DI like `IncidentAppService` (`IncidentAppService.cs:11`), registered `builder.Services.AddScoped<PostmortemAppService>();` in `Program.cs` (alongside `:178-196`). Methods: `GetAllAsync`, `GetByIdAsync`, `CreateAsync` (inserts an empty field value per active definition, snapshots owner name), `UpdateAsync` (report metadata + field values), `PublishAsync`/`UnpublishAsync` (mirroring `IncidentAppService.PublishAsync/UnpublishAsync` at `:325,349`), `DeleteAsync`, `LinkIncidentAsync`/`UnlinkIncidentAsync`, `AddTimelineEntryAsync`/`UpdateTimelineEntryAsync`/`DeleteTimelineEntryAsync`.
- **DTOs** — `record`s in `src/Piro.Application/DTOs/PostmortemDto.cs`: `PostmortemDto` (response, includes field values joined to their definitions + merged timeline + referenced-incident summaries), `PostmortemFieldDefinitionDto`, `PostmortemFieldValueDto`, `PostmortemTimelineEntryDto`, `PostmortemListItemDto`, `CreatePostmortemRequest`, `UpdatePostmortemRequest` (carries the field values to write), `LinkIncidentRequest`, `CreateTimelineEntryRequest`. Entity→DTO mapping in a static extension `src/Piro.Application/Extensions/PostmortemExtensions.cs`, matching `IncidentExtensions.cs`.
- **Controller** — new `src/Piro.Api/Controllers/PostmortemsController.cs`, `[Route("api/v1/postmortems")]`, under the same `[Authorize(Roles = "Owner,Admin,Member")]` as `IncidentsController` (`:14`). Routes:
  - `GET  /api/v1/postmortems` — list (paged, `PostmortemListItemDto`)
  - `GET  /api/v1/postmortems/{id:int}`
  - `POST /api/v1/postmortems`
  - `PUT  /api/v1/postmortems/{id:int}`
  - `POST /api/v1/postmortems/{id:int}/publish` · `/unpublish`
  - `DELETE /api/v1/postmortems/{id:int}`
  - `POST /api/v1/postmortems/{id:int}/incidents` · `DELETE …/incidents/{incidentId:int}`
  - `POST /api/v1/postmortems/{id:int}/timeline` · `PUT`/`DELETE …/timeline/{entryId:int}`

  Actor/author names come from claims off `ControllerBase.User` exactly as the incident acknowledge action does — `User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Unknown"` (`IncidentsController.cs:100`). The review-owner is resolved from the request's `reviewOwnerUserId`, and its display name is snapshotted into `ReviewOwnerName` at that point (§4.2, §7).

- **Admin UI** (`apps/admin`) — a "Postmortems" section: a list page, a create form (name, owner picker, impact window), and an editor mirroring PagerDuty's layout (owner + status + impact window header, incident-linking data-sources block, merged timeline panel, and the analysis fields rendered from the field definitions — each value edited against its definition's heading, help text, and type). API types regenerated from the OpenAPI spec via `pnpm run generate:api-types` (per `AGENTS.md`); one component per file per the admin conventions. This is the `frontend` half of issue #63.

### 4.6 `PostmortemIncident` — the N:M link

New junction entity `src/Piro.Domain/Entities/PostmortemIncident.cs`, modeled on the existing `IncidentService` junction (`src/Piro.Domain/Entities/IncidentService.cs`):

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `PostmortemId` | `int` | no | composite PK part, FK to `Postmortem` |
| `IncidentId` | `int` | no | composite PK part, FK to `Incident` |
| `CreatedAt` | `DateTime` | no | audit |

`Incident` gains a nav collection `ICollection<PostmortemIncident> PostmortemIncidents` (alongside its existing junction navs at `Incident.cs:47-50`) — but **no scalar column** is added to `Incidents` (unlike the earlier 1:1 idea; the N:M link lives entirely in the join table). The impact window on the report is a *suggestion filter* for which incidents to offer, not a hard constraint — the author links incidents explicitly, matching PagerDuty's "select incident between impact times" + manual add.

### 4.7 On-delete behavior for the review owner

`Postmortem.ReviewOwnerUserId` → `AppUser` is a **nullable FK with `ON DELETE SET NULL`**. When the owning `AppUser` is deleted, the FK nulls out but the postmortem survives and still displays `ReviewOwnerName` (the snapshot taken at assign time, §4.2). This satisfies "preserve the postmortem even if the user is deleted" without either (a) blocking user deletion (`RESTRICT`) or (b) losing the attribution entirely. The snapshot is refreshed whenever the owner is reassigned via `UpdateAsync`. Configured in `PostmortemConfiguration.cs` with `.OnDelete(DeleteBehavior.SetNull)`.

### 4.8 EF configuration

New configuration classes under `src/Piro.Infrastructure/Persistence/Configurations/` (`internal ... : IEntityTypeConfiguration<>`), auto-discovered by `ApplyConfigurationsFromAssembly` (`PiroDbContext.cs:55`):

- `PostmortemConfiguration` — `ReviewOwnerUserId` FK `SetNull` (§4.7); enums `.HasConversion<string>()` (`IncidentConfiguration.cs:18`).
- `PostmortemFieldDefinitionConfiguration` — unique index on `Key`; `FieldType` string conversion; seeds the eight `IsSystem` definitions via `HasData` (§4.3).
- `PostmortemFieldValueConfiguration` — `PostmortemId` cascade delete; `FieldDefinitionId` FK `RESTRICT` (a definition in use can't be hard-deleted); unique index on `(PostmortemId, FieldDefinitionId)`.
- `PostmortemTimelineEntryConfiguration` — `PostmortemId` cascade delete.
- `PostmortemIncidentConfiguration` — composite key `(PostmortemId, IncidentId)`; both FKs cascade from their parents (matching `IncidentService`).
- New DbSets on `PiroDbContext`: `Postmortems`, `PostmortemFieldDefinitions`, `PostmortemFieldValues`, `PostmortemTimelineEntries`, `PostmortemIncidents`, alongside the existing expression-bodied sets (`PiroDbContext.cs:18-32`).

### 4.9 What does NOT change

- **`IncidentTimelineEvent` / `IncidentImpactChange` / `Alert` / `IncidentService`** — untouched. The report *reads* them; it adds no fields and no copies. Manual annotations go into `PostmortemTimelineEntry`, never back onto the incident.
- **`Incident`** — gains only a nav collection (`PostmortemIncidents`); **no scalar column** is added to the `Incidents` table. Its status ladder, acknowledge/publish flows, and merge semantics are untouched (§2). Referencing an incident in a report changes nothing about the incident.
- **`AppUser` / roles / auth** — the `AppUser` entity is unchanged (the FK lives on `Postmortem`). No new role or permission; the feature rides the existing `Owner,Admin,Member` authorization.
- **`INotificationDispatcher` / alert pipeline / Quartz jobs** — untouched. No dispatch, no scheduling, no external I/O.
- **Public web app (`apps/web`)** — untouched in Phases 1–2 (§6).

## 5. Data / schema scope

One migration under `src/Piro.Infrastructure/Migrations/` (flat directory, run on startup via `db.Database.Migrate()` at `Program.cs:205`), EF-default timestamp naming (e.g. `AddPostmortems`):

**New tables:**
- `Postmortems` — columns per §4.2. FK `ReviewOwnerUserId` → `AspNetUsers` with `ON DELETE SET NULL`.
- `PostmortemFieldDefinitions` — per §4.3. Unique index on `Key`. Seeded with the eight standard sections via `HasData`.
- `PostmortemFieldValues` — per §4.3. FK + cascade to `Postmortems`; FK `RESTRICT` to `PostmortemFieldDefinitions`; unique `(PostmortemId, FieldDefinitionId)`.
- `PostmortemTimelineEntries` — per §4.4. FK + cascade to `Postmortems`.
- `PostmortemIncidents` — per §4.6. Composite PK `(PostmortemId, IncidentId)`, FKs cascade from `Postmortems` and `Incidents`.

**New enums** (persisted as string columns, no lookup tables):
- `PostmortemStatus` — `Draft`, `Published`.
- `PostmortemFieldType` — `Text`, `LongText`, `Date`, `Select`.

**Modified table:** none. `Incidents` gets a navigation only (the link lives in the `PostmortemIncidents` join table); no column is added to any existing table.

**Explicitly unchanged:** `IncidentTimelineEvents`, `IncidentImpactChanges`, `IncidentServices`, `Alerts`, `AspNetUsers`/`AspNetRoles` (Identity schema), `Services`, `Checks`, `Pages`, `Maintenances`, `Integrations`, and every enum other than the two new ones.

## 6. Phased plan

Each phase is independently shippable.

1. **Phase 1 — Report aggregate + internal CRUD.** `Postmortem`, `PostmortemFieldDefinition` (seeded, non-editable), `PostmortemFieldValue`, `PostmortemIncident` entities, the `PostmortemStatus`/`PostmortemFieldType` enums, the migration (with the eight seeded definitions), repository, `PostmortemAppService`, the `PostmortemsController` routes for report CRUD + publish + incident linking, and empty-value seeding on create. Admin UI: list, create, and the field editor (rendered from the definitions) with incident linking. Timeline shows only the derived incident events. This closes the substance of issue #63 (root cause, impact, action items, grouped against incidents) for internal use.
2. **Phase 2 — Report-owned timeline annotations + merge.** Add `PostmortemTimelineEntry` (§4.4), its CRUD routes, and the chronological merge of derived incident events with report annotations in `PostmortemDto`. Also: default `ImpactStartAt`/`ImpactEndAt`/incident suggestions from the impact window, refresh `ReviewOwnerName` on reassignment, and richer `PostmortemFieldType` input widgets/validation beyond the plain textarea (`Date`, `Select`). Separated because the CRUD shell and section analysis are useful before the merged-timeline UX, which is the part most likely to want iteration.
3. **Phase 3 — User-defined fields + public publishing (both optional).** Two independent extensions the table-based field model already accommodates: (a) let admins add/edit/reorder/deactivate `PostmortemFieldDefinition`s (`IsSystem = false` custom fields) through the admin UI — zero schema change, since the definition table was designed for it (§4.3); and (b) surface a published postmortem (or a redacted public subset of its fields) on the public status page (`apps/web`), gated by an explicit public-visibility flag distinct from the internal `Draft/Published` status. (b) is deferred because it introduces redaction of internal root-cause detail and a public/internal content split (§2); issue #63 frames it as explicitly optional.

## 7. Alternatives considered

- **1:1 postmortem anchored to a single incident (sub-resource of incidents).** Rejected in design discussion in favor of the standalone N:M report — a real review often spans several correlated incidents, and PagerDuty (which issue #63's reporter references) models it as a free-standing report with its own name and impact window. The 1:1 model would force one review per incident and couldn't express a multi-incident retrospective. Cost of the chosen model: a join table and a top-level controller instead of a sub-resource — acceptable for the expressiveness gained.
- **Owner as a denormalized display-name string only (the existing incident-domain convention: `AcknowledgedBy`, `ActorName`).** Rejected as the *sole* mechanism — a postmortem's owner is a real accountability subject the UI wants to render as a proper user (avatar, link, later maybe notifications), which a bare string can't support. But a hard FK *alone* would lose attribution when the user is deleted. Chosen resolution: **FK + snapshot name** (§4.2, §4.7) — live link while the user exists, preserved attribution after deletion. This is a deliberate, narrow departure from the string-only convention, justified by the owner being a first-class subject rather than incidental attribution.
- **`ON DELETE RESTRICT` / required owner.** Rejected — blocking `AppUser` deletion because a historical postmortem references them is the wrong trade-off; postmortems are permanent records and users come and go. `SET NULL` + snapshot preserves the record without constraining user lifecycle (§4.7).
- **One column per standard section on the `Postmortem` row.** Rejected — hard-codes the template into the schema, making "add/rename a section" a migration and precluding custom fields entirely.
- **A single `PostmortemSection` row-per-section table with a `Kind` enum (the eight kinds + `Custom`).** This was the first cut, and it does make sections rows instead of columns. Rejected in favor of the **definition + value** split (§4.3) because the `Kind` enum still hard-codes the field set in code — adding a user-defined field would mean touching the enum. Splitting the *definition* (which sections exist) from the *value* (what a report wrote) puts the field set in a data table, so a future phase can let admins define custom fields with no code or schema change (Phase 3a, §6). The cost is one extra table and a join on read — worth it for the extensibility, which is an explicit goal.
- **Writing manual annotations back onto the incident as `CommentPosted` timeline events (single global timeline).** Rejected — annotations are review-analysis notes that belong to the report, not operational incident updates, and a report may reference several incidents (which one would the note attach to?). Keeping annotations in `PostmortemTimelineEntry` (§4.4) scopes them to the review while still merging them into the displayed timeline.
- **A dedicated copied timeline table on the report.** Rejected — it would drift from the incidents' real `IncidentTimelineEvent` history. Deriving at read time (§4.4) keeps a single source of truth for the machine-recorded facts.

## 8. Risks

- **Derived-timeline drift after publish.** Because the factual timeline is *derived* from the referenced incidents (§4.4), editing an incident's events after the postmortem is published changes what the report shows retroactively. Intentional (single source of truth), but can surprise a reader expecting a published postmortem to be a frozen snapshot. If frozen snapshots become a requirement, Phase 3's public-publish is the natural place to materialize one; internal reports stay live-linked.
- **A published postmortem is still editable.** Phase 1 keeps `Published` reports editable by `Owner`/`Admin` (mirroring reversible incident publish). If a team treats "Published" as the immutable record of record, silent post-publish edits are a governance risk. Deferred by design — a later phase can add a post-publish lock or edit-audit trail once real usage of the draft/publish boundary is known.
- **Orphaned owner FK vs. snapshot divergence.** After the owning `AppUser` is deleted, `ReviewOwnerUserId` is null but `ReviewOwnerName` still shows the old name (§4.7). If the user is instead *renamed* (not deleted), the live FK resolves to the new name while any UI relying on the snapshot shows the old one — the snapshot is refreshed only on reassignment (Phase 2). Callers must decide which to display; the DTO exposes both.
- **Same incident referenced by multiple reports.** The N:M model (§4.6) permits an incident to appear in more than one postmortem. Usually fine (a broad quarterly review may cite an incident already covered by its own postmortem), but the UI should make an incident's existing report memberships visible so authors don't unknowingly double-review. Not constrained at the schema level by choice.
- **Impact window vs. linked incidents can disagree.** The impact window is only a suggestion filter (§4.6); an author can link an incident outside the stated window, or leave the window blank. The report is the author's assertion, not a derived invariant — acceptable, but the "timeline from X to Y" header (as in PagerDuty's UI) should reflect the actual linked incidents' span when the window is empty, to avoid an empty/nonsensical range.
```

