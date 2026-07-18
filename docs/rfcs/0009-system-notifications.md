# RFC 0009 — System notifications: operational alerts delivered to administrators

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-17

## 1. Problem

Piro can tell an on-call engineer that *a monitored service* is down. It cannot tell an administrator that *Piro itself* is broken.

Every notification Piro sends today originates from an `Alert` row and flows through one path: `AlertEvaluationService` records the alert after a check runs, and `EscalationCheckerService` walks the service's `EscalationPolicy` and notifies on-call users via `INotificationDispatcher.DispatchPersonalAsync` (`src/Piro.Application/Services/EscalationCheckerService.cs:180`). This pipeline is entirely about *a service's* health, escalated to *that service's* on-call rotation. There is no concept of an operational event about the platform, and no audience of administrators to receive one: no system-event entity, no system-alert enum, no non-check-anchored notification path — nothing.

This gap is not hypothetical; RFC 0008 walked straight into it. Two of its risks (0008 §8) are only mitigated if Piro can email an administrator:

- **A check becomes unschedulable** (no worker matches its selector, 0008 §4.6). RFC 0008's mitigation was "always email a system/admin address regardless of on-call policy" — but that address, and the mechanism to reach it, do not exist. Without them an unschedulable check is silent exactly when a service has no `EscalationPolicy` (`src/Piro.Domain/Entities/Service.cs:51`), which is the case the mitigation was for.
- **Stored system-tag drift via a second write path** (a YAML import writing `Check.Type` without reconciling `piro:check-type`, 0008 §8). This is a *Piro-internal inconsistency* — not tied to any one service's uptime — so it has no `Alert`, no on-call, and today no way to surface at all.

Both are the same missing primitive: **a notification about Piro's own operational state, delivered to administrators, out-of-band from service on-call.** This RFC introduces it — and, critically, structures it so that the component *detecting* an operational condition never decides *what to do about it*. The scheduler that finds an unschedulable check should announce that fact, not choose a severity, a channel, and an email body. That separation — event emission from response policy — is the spine of the design.

## 2. Non-goals

- **Reusing the escalation machinery.** System notifications are not escalated, not acknowledged, not laddered. `EscalationPolicy`, on-call schedules, `UserNotificationPreference`-driven routing, and the retry ladder (`EscalationCheckerService`) are for service incidents; forcing platform events through them would conflate "a service is down, page the rotation" with "Piro's SMTP is misconfigured, tell an admin." This is a separate, simpler pipeline (§3).
- **A dynamic, admin-configurable routing-rules engine.** The response policy (which severity, which channels, in-app vs email) lives in code, in one handler per event type (§4.2) — hardcoded in v1. The design leaves the *seam* for that policy to become data-driven later (a rules table mapping event/severity → channels), but building that engine is future work, not v1.
- **Exceptions as the notification transport.** Operational events are carried as domain events, not thrown as exceptions. A real unhandled exception (a Quartz job that crashes) is a *source* of a system event — translated into one by a global listener (§4.6) — but exceptions are never the mechanism by which a non-failing condition (an unschedulable check, which does not abort the scheduler) signals a notification. Using `throw` for that is control-flow abuse and is explicitly rejected (§7).
- **Touching the existing alert path.** `AlertNotificationContext`, `DispatchPersonalAsync`, `AlertMessageTemplates`, `EscalationCheckerService`, and every dispatcher under `src/Piro.Infrastructure/Alerts/` are left exactly as they are (§4.8).
- **Multi-channel delivery in v1.** Only email is delivered in v1. The design is multi-channel-*ready* through a seam (§4.4), but Telegram/SMS/etc. for system notifications are future work, not designed here.
- **A public-facing surface.** System notifications are internal operational signals for administrators. They never appear on the public status page (`apps/web`) and never affect `ServiceStatus` or `PublicStatus`.
- **Designing every possible system event.** v1 ships the framework plus the two events RFC 0008 needs. Future events (worker offline, migration failure, SMTP misconfiguration, disk pressure) are named as motivation but not modeled (§4.4).
- **Refactoring dispatchers to be channel-agnostic.** That refactor is desirable and would let the seam collapse (§4.4), but it is out of scope; v1 works with dispatchers as they are.

## 3. Design principle

**Detection emits a fact; a handler decides the response; a delivery pipeline executes it — three layers, each replaceable without the others.** An emitter (the scheduler, the tag reconciler, a crashed job) publishes a typed *system event* describing only what happened. A *handler* for that event type owns the policy — severity, which actions, what the human-readable notification says — hardcoded in v1, a seam for dynamic rules later. The handler drives a *delivery pipeline* that reuses only the lowest send layer ("deliver this text to this handle"), never the alert-shaped machinery above it: a neutral model (not `AlertNotificationContext`), a channel seam over `IEmailService` rather than the alert dispatcher, an audience defined by role membership rather than on-call schedules, and dedup borrowed in spirit from `AlertLifecycleService` on a new table so the alert path is untouched. The event transport is not invented here — it reuses Piro's existing in-process `Channel<T>` + hosted-service consumer pattern (§4.1).

## 4. Design

```
  ── EMITTERS (announce a fact, decide nothing) ───────────────────────────────
  scheduler: matching workers = ∅   → writer.Publish(new CheckUnschedulableEvent(checkId, selector))
  tag reconciler: stored piro:* drift → writer.Publish(new SystemTagDriftedEvent(entity, key))
  Quartz job throws                  → global IJobListener → writer.Publish(new BackgroundJobFailedEvent(...))
        │
        ▼   Channel<ISystemEvent>  (singleton, unbounded — SAME pattern as CheckStatusChangedEvent,
        │                            InfrastructureServiceExtensions.cs:129)
        ▼
  SystemEventDrainHostedService : BackgroundService     (mirrors StatusDrainHostedService.cs:19-38)
     await foreach (evt in reader.ReadAllAsync)
        └─ scope = CreateAsyncScope() → resolve ISystemEventHandler<TEvent> for evt's type
        ▼
  ── POLICY (one handler per event type — decides severity, actions, wording) ──
  CheckUnschedulableHandler:  severity = Warning;  actions = [Notify]   // hardcoded v1; seam for dynamic rules
     builds title/body/fingerprint  →  ISystemNotificationService.RaiseAsync(...)
        │
        ▼
  ── DELIVERY (dedup + persist + fan-out; reused by every handler) ─────────────
  RaiseAsync: dedup (Category,Fingerprint)? ─ yes → OccurrenceCount++, no send   (AlertLifecycleService.cs:38-43)
              no → persist SystemNotification row
                 → audience: SiteConfig.SystemNotificationAudience → GetUsersInRoleAsync (SetupController.cs:239)
                 → per recipient: ISystemNotificationChannel.SendAsync(email, subject, body)
                                        └─ v1 EmailSystemChannel → IEmailService.SendAsync (IEmailService.cs:6)

   ── untouched: Alert / AlertNotificationContext / DispatchPersonalAsync / EscalationCheckerService ──
```

Three layers, read top-to-bottom: **emitters** publish a typed event and nothing more; a per-type **handler** decides the response policy; a shared **delivery** service executes it. Each is independently replaceable — a new emitter needs no handler change, a policy change touches only a handler, a new channel touches only delivery.

### 4.1 System events and their transport

An emitter's entire responsibility is to publish a fact. A system event is a small record implementing a marker interface:

```csharp
// src/Piro.Application/Models/SystemEvents/ISystemEvent.cs
public interface ISystemEvent { }

// one record per fact — carries only what happened, no severity, no wording, no channel
public record CheckUnschedulableEvent(int CheckId, string SelectorSummary) : ISystemEvent;
public record CheckSchedulableAgainEvent(int CheckId) : ISystemEvent;         // the inverse (resolve)
public record SystemTagDriftedEvent(string Entity, string Key) : ISystemEvent;
public record BackgroundJobFailedEvent(string JobKey, string Error) : ISystemEvent;
```

**The transport is not invented here.** Piro already runs an in-process event pipeline exactly this shape: a singleton unbounded `Channel<CheckStatusChangedEvent>` (`src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:129`) that `CheckResultIngesterService` writes to and `StatusDrainHostedService` drains in a `BackgroundService` loop — `await foreach (var evt in channel.Reader.ReadAllAsync(...))`, opening a DI scope per event to resolve scoped services (`src/Piro.Infrastructure/Jobs/StatusDrainHostedService.cs:19-38`). This RFC reuses that exact pattern for `Channel<ISystemEvent>`:

- **Emitters** take the `ChannelWriter<ISystemEvent>` (or a thin `ISystemEventPublisher` wrapper over it) and call `Publish(evt)`. This is fire-and-forget and non-blocking — publishing an event never stalls or aborts the emitter (the scheduler keeps scheduling other checks), which is the core reason events, not exceptions, are the transport (§7).
- **`SystemEventDrainHostedService`** (new, modeled on `StatusDrainHostedService`) drains the channel, and for each event opens a scope (`CreateAsyncScope()`) and dispatches to the handler(s) registered for that event's runtime type.

Using a `Channel` rather than a synchronous in-line call also means an emitter on a hot path (the scheduler tick) pays nothing for delivery — it drops the event and moves on; all the dedup/DB/email work happens on the drain thread.

### 4.2 Handlers — where the response policy lives

For each event type there is one handler that decides *everything about the response*: severity, which actions to take, and the human-readable title/body. This is the layer that keeps the scheduler from knowing that "unschedulable means Warning, by email."

```csharp
// src/Piro.Application/Interfaces/ISystemEventHandler.cs
public interface ISystemEventHandler<in TEvent> where TEvent : ISystemEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}

// src/Piro.Application/SystemEvents/CheckUnschedulableHandler.cs
internal class CheckUnschedulableHandler(ISystemNotificationService notifications)
    : ISystemEventHandler<CheckUnschedulableEvent>
{
    public Task HandleAsync(CheckUnschedulableEvent e, CancellationToken ct)
        => notifications.RaiseAsync(
               category:    SystemNotificationCategory.CheckUnschedulable,
               severity:    SystemNotificationSeverity.Warning,          // ← policy: hardcoded in v1
               title:       "Check cannot be scheduled",
               body:        $"Check {e.CheckId} matches no live worker for selector {e.SelectorSummary}.",
               fingerprint: e.CheckId.ToString(),                        // ← stable identity for dedup (§4.5)
               ct);
}
```

Handlers are registered as `IEnumerable<ISystemEventHandler<TEvent>>` — the same DI-collection pattern Piro already uses for `IEnumerable<INotificationDispatcher>` (`src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:204-214`). The drain service dispatches an event to every handler registered for its type (allowing, later, more than one action per event — e.g. a handler that also writes an audit row).

**The severity and the action list are hardcoded here in v1, and this is the seam for dynamic policy.** Today `CheckUnschedulableHandler` names `Warning` and "notify" directly in code. The migration path to admin-configurable rules is: replace the literals with a lookup into a rules table (`event type → severity, channels`) read from config — the handler becomes a thin adapter over a rules evaluator, and *nothing else in the pipeline changes*, because emitters and delivery never knew the policy in the first place. Building that rules engine is future work (§2); v1 ships the seam, not the engine.

### 4.3 `ISystemNotificationService` — the delivery pipeline

The handler's actions run through one delivery service — the un-escalated analogue of `EscalationCheckerService`, but linear and one-shot. It owns dedup, persistence, audience resolution, and fan-out; it does **not** decide severity or wording (the handler already did).

```csharp
// src/Piro.Application/Interfaces/ISystemNotificationService.cs
public interface ISystemNotificationService
{
    Task RaiseAsync(SystemNotificationCategory category, SystemNotificationSeverity severity,
                    string title, string body, string fingerprint, CancellationToken ct = default);

    // Called by the inverse-event handler when the condition clears — sets ResolvedAt, does not re-notify.
    // Mirrors AlertLifecycleService.ResolveActiveAlertAsync.
    Task ResolveAsync(SystemNotificationCategory category, string fingerprint, CancellationToken ct = default);
}
```

`RaiseAsync` is the bottom band of the diagram: dedup (§4.5) → (if new) persist → resolve audience → deliver via the channel seam (§4.4). It is a pure executor of an already-decided notification.

### 4.4 The `SystemNotification` entity (persisted) and the send seam

A system notification is **persisted**, not fire-and-forget — persistence buys dedup of recurring events (§4.5), an audit trail, and a foundation for a future in-app admin feed. The row is the record; delivery is a side effect of creating a *new* one.

```csharp
// src/Piro.Domain/Entities/SystemNotification.cs
public class SystemNotification
{
    public int Id { get; set; }
    public SystemNotificationCategory Category { get; set; }   // CheckUnschedulable | TagReconciliation | ...
    public SystemNotificationSeverity Severity { get; set; }   // Info | Warning | Critical
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;    // dedup key within a category (§4.5)

    public DateTimeOffset FiredAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }            // null while the condition persists
    public int OccurrenceCount { get; set; } = 1;              // folded repeats, mirrors Alert.OccurrenceCount
    public DateTimeOffset? AcknowledgedAt { get; set; }        // admin dismissed it (future in-app feed)
}

public enum SystemNotificationSeverity { Info, Warning, Critical }

public enum SystemNotificationCategory
{
    CheckUnschedulable,     // RFC 0008 §4.6 — no worker matches a check's selector
    TagReconciliation,      // RFC 0008 §8   — stored piro:* tag drift / YAML import inconsistency
    // future (named, not designed): WorkerOffline, MigrationFailure, EmailMisconfigured, ...
}
```

`Category` labels the *kind* of condition (for dedup scoping and the future in-app feed's filtering); `Severity` is set by the handler as policy (§4.2). v1 wires two categories, each fed by an event (§4.1) and shaped by a handler (§4.2): **`CheckUnschedulable`** — the Part B scheduler's `CheckUnschedulableEvent`, complementary to the orphan `Alert` with `AlertSource.SchedulingFailure` (RFC 0008 §4.6): the `Alert` drives on-call *if a policy exists*, the `SystemNotification` guarantees an admin is told *even if it does not* — and **`TagReconciliation`** — the reconciler's `SystemTagDriftedEvent` (RFC 0008 §8), surfacing silent internal inconsistency that has no other channel. A future category is a new enum value + event record + handler; the delivery pipeline is untouched.

It mirrors `Alert`'s dedup shape (`Fingerprint`/`OccurrenceCount`/`FiredAt`/`ResolvedAt`, cf. `src/Piro.Domain/Entities/Alert.cs`) but is a *separate* entity with none of `Alert`'s check/service/escalation coupling — no `CheckId`, `ServiceId`, or `EscalationPolicyId`. A system notification is about the platform; pinning it to a service is the alert-semantics leak this RFC avoids. Where an event concerns a specific entity, that reference lives in the human-readable `Body`/`Title`, not as a foreign key, so the entity never joins the service-status graph. EF wiring is conventional: `DbSet<SystemNotification>` on `PiroDbContext` (`src/Piro.Infrastructure/Persistence/PiroDbContext.cs:18-53`), an auto-discovered `IEntityTypeConfiguration` (`:59`), a startup-applied migration (`src/Piro.Api/Program.cs:205`), and a partial index on `(Category, Fingerprint) WHERE ResolvedAt IS NULL` for the dedup lookup.

**The send seam — `ISystemNotificationChannel`.** Delivery never names a concrete channel:

```csharp
// src/Piro.Application/Interfaces/ISystemNotificationChannel.cs
public interface ISystemNotificationChannel
{
    Task SendAsync(string handle, string subject, string body, CancellationToken ct = default);
}

// src/Piro.Infrastructure/Notifications/EmailSystemChannel.cs — the ONLY v1 implementation
internal class EmailSystemChannel(IEmailService emailService) : ISystemNotificationChannel
{
    public Task SendAsync(string handle, string subject, string body, CancellationToken ct = default)
        => emailService.SendAsync(handle, subject, body, ct);   // IEmailService.cs:6
}
```

- **Why a new seam, not `INotificationDispatcher.SendPersonalMessageAsync`.** That plain-text method (`src/Piro.Application/Interfaces/INotificationDispatcher.cs:27`) is the right *conceptual* layer — bare string, already used standalone for verification codes (`src/Piro.Application/Services/UserManagementService.cs:369`) — but for email it is unusable: `EmailDispatcher.SendPersonalMessageAsync` hardcodes the subject to `"Your Piro verification code"` and wraps the body in `<p>{message}</p>` (`src/Piro.Infrastructure/Alerts/EmailDispatcher.cs:28-34`), giving no subject control. `IEmailService.SendAsync` takes an explicit subject + HTML body, so `EmailSystemChannel` wraps that, beside `SendInvitationAsync` (`src/Piro.Application/Interfaces/IEmailService.cs:12`) as another purpose-built transactional send.
- **Why the seam earns its place in an email-only v1.** Adding Telegram later is registering a `TelegramSystemChannel` wrapping `telegramDispatcher.SendPersonalMessageAsync` — the already-channel-agnostic bare-text method — with no change to the pipeline. When dispatchers are eventually refactored to be channel-agnostic (§2), `ISystemNotificationChannel` collapses into them untouched. It is the one-line insurance that "email only" is a delivery choice, not an architecture lock-in.

`EmailSystemChannel` registers beside the existing dispatchers (`src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:204-214`); `IEmailService` is already registered (`:60`) and no-ops silently when email is unconfigured — so an unconfigured instance degrades to "persisted but not delivered," which the row still records.

### 4.5 Dedup and rate-limiting

*(the entity above enables this; the policy below is why persistence is mandatory)*

A recurring condition must fold into one row, not one email per tick. `RaiseAsync` reuses `AlertLifecycleService`'s strategy in spirit (`src/Piro.Application/Services/AlertLifecycleService.cs:35-43`): look up the active (`ResolvedAt == null`) `SystemNotification` for the same `(Category, Fingerprint)`; if it exists, **bump `OccurrenceCount` and return without sending**; only a genuinely new `(Category, Fingerprint)` persists and delivers.

The `Fingerprint` — set by the handler (§4.2), not the emitter — is what makes this precise. For an unschedulable check it is the check id (re-detecting the *same* check folds; a *different* check is a new notification); for tag drift it is entity+key. This mirrors `Alert.MessageFingerprint` exactly: a failing 1-minute check produces one `Alert` with a rising `OccurrenceCount`, not hundreds (0008 §8 relied on this), and a system condition behaves the same. When the condition clears, the inverse event (`CheckSchedulableAgainEvent`) drives a handler that calls `ResolveAsync`, closing the row — so a later recurrence is a fresh notification, not a silently-folded one. This is why persistence (§4.4) is not optional: dedup needs a durable record of what is currently active; a fire-and-forget design would email every tick or need an in-memory cache that resets on restart.

### 4.6 Event sources: emitters and the exception bridge

Events enter the channel from two kinds of source:

- **Explicit emitters** publish a domain event at the point they detect a condition. The RFC 0008 Part B scheduler publishes `CheckUnschedulableEvent` when the matching-worker set is empty and `CheckSchedulableAgainEvent` when it recovers; the tag-reconciliation component publishes `SystemTagDriftedEvent`. These are the normal case — a known operational condition, announced.
- **The exception bridge.** A *real* unhandled exception in a background job is itself an operational event, but the code that throws cannot politely publish. Piro's Quartz setup (`src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:133-163`) supports a global `IJobListener`; this RFC adds one whose `JobWasExecuted` inspects the `JobExecutionException` and, when present, publishes a `BackgroundJobFailedEvent`. This is the one place exceptions meet the system: a caught, already-failed exception is *translated into* an event — not thrown as the signalling mechanism. A condition that does not abort its operation (an unschedulable check) is never expressed as an exception (§7).

Both sources feed the same channel and the same handler dispatch — the drain service doesn't care how an event was born.

### 4.7 Recipients, audience threshold, and the admin UI

**Audience by named role sets — because there is no role hierarchy.** Roles are flat AspNetIdentity roles (`AppRole : IdentityRole<int>`, `src/Piro.Domain/Entities/AppRole.cs:10`); `AppRole` has no `Level`/`Rank` field, and "admin and above" exists in the codebase only as the literal string `"Owner,Admin"` repeated across `[Authorize]` attributes (e.g. `src/Piro.Api/Controllers/SiteController.cs:30`). The built-in roles are `Owner`, `Admin`, `Member`, `Viewer` (`src/Piro.Domain/Entities/AppRole.cs:12` doc). Since there is nothing to compare `>=` against, the audience is defined as one of three explicit, named sets:

| Audience setting | Role set | Use |
|---|---|---|
| `Owners` | `{Owner}` | narrowest — only account owners |
| `Admins` (**default**) | `{Owner, Admin}` | matches the existing `"Owner,Admin"` authorization convention |
| `Members` | `{Owner, Admin, Member}` | broadest — everyone but Viewers |

Resolution unions Identity queries: `UserManager.GetUsersInRoleAsync("Owner")` (∪ `"Admin"` ∪ `"Member"` per the setting), the exact API already used at `src/Piro.Api/Controllers/SetupController.cs:239` and `src/Piro.Application/Services/UserManagementService.cs:176`. Each recipient's handle is the confirmed `AppUser.Email` (from `IdentityUser`). **The audience is never empty:** the last Owner cannot be deleted or demoted (`src/Piro.Application/Services/UserManagementService.cs:174-178`) and is created in setup with a confirmed email and a verified `IsAccountFallback` email preference (`src/Piro.Api/Controllers/SetupController.cs:189-197`) — so `Owners` always resolves to at least one reachable admin.

**Role-name constants.** No shared role-name constants exist today — only a controller-local `const string OwnerRole = "Owner"` (`src/Piro.Api/Controllers/SetupController.cs:31`); everywhere else the names are bare literals. This work introduces a small shared `Roles` static class (`Owner`/`Admin`/`Member`/`Viewer`) in the Domain (or Application) layer and uses it for audience resolution, rather than adding a fourth spelling of `"Admin"` to the codebase. Migrating the existing `[Authorize]` literals to it is a nice-to-have, not required by this RFC.

**Configuration UI (`apps/admin`).** The audience threshold is one setting, surfaced on the existing **Site settings** screen in the admin SPA (the same screen backed by `SiteController`, `src/Piro.Api/Controllers/SiteController.cs`, and the `SiteConfig` record). It is a single labelled **select** control — "Send system notifications to" — with three options (`Owners` / `Admins` / `Members`), defaulting to `Admins`, with helper text naming which roles each covers. No new page; it is a new field in the settings form. Persistence is through `ISiteConfigRepository` (§5). There is no per-notification UI in v1 (the persisted rows anticipate a future in-app admin feed, §7, which would be its own screen); v1's only user-facing surface is this one setting.

### 4.8 What does NOT change

- **The alert pipeline, end to end.** `AlertNotificationContext` (`src/Piro.Application/Models/AlertNotificationContext.cs`), `INotificationDispatcher.DispatchPersonalAsync`, `AlertMessageTemplates` and its Scriban templates, `EscalationCheckerService`, `AlertEvaluationService`, and `AlertLifecycleService` are untouched. System notifications are a parallel, simpler path — they reuse the *idea* of fingerprint dedup, not the alert code.
- **The dispatchers.** Every class under `src/Piro.Infrastructure/Alerts/` is unchanged. `EmailSystemChannel` wraps `IEmailService` directly and does not go through `EmailDispatcher`.
- **`IEmailService`.** Reused as-is via its existing `SendAsync` (`src/Piro.Application/Interfaces/IEmailService.cs:6`); no new method is required on it (a dedicated `SendSystemNotificationAsync` was considered and rejected, §7).
- **On-call / escalation model.** `EscalationPolicy`, `OnCallSchedule`, `UserNotificationPreference`, and the escalation ladder are irrelevant to system notifications and are not read or written by this pipeline.
- **Public surface and status.** `apps/web`, `ServiceStatus`, `Service.PublicStatus`, and `CheckDataPoint` are untouched — a system notification is never downtime.
- **Roles/permissions model.** No change to `AppRole`/`AppUser`/Identity; the `Roles` constants are a naming convenience over the existing role strings, not a schema change.

## 5. Data / schema scope

New table (one migration, auto-applied on startup — `db.Database.Migrate()`, `src/Piro.Api/Program.cs:205`):

- **`SystemNotifications`** — `Id` PK; `Category` (enum-as-string); `Severity` (enum-as-string); `Title`, `Body`, `Fingerprint` (required); `FiredAt`, `ResolvedAt?`, `OccurrenceCount`, `AcknowledgedAt?`. Partial index on `(Category, Fingerprint) WHERE ResolvedAt IS NULL` for the dedup lookup (§4.4).

New enums:

- **`SystemNotificationCategory`** — `{ CheckUnschedulable, TagReconciliation }` (extensible, §4.4).
- **`ISystemEvent`** marker + the event records (`CheckUnschedulableEvent`, `CheckSchedulableAgainEvent`, `SystemTagDriftedEvent`, `BackgroundJobFailedEvent`) — plain records, no schema (§4.1).
- **`SystemNotificationSeverity`** — `{ Info, Warning, Critical }`.

New config key (SiteData key-value store):

- Add **`system:notification_audience`** to `SiteDataKeys.All` (`src/Piro.Application/Constants/SiteDataKeys.cs:16-26`) — required, since `SiteConfigRepository.GetAsync` only loads keys present in `All` (`src/Piro.Infrastructure/Persistence/Repositories/SiteConfigRepository.cs:10-27`) — and add a mapped `SystemNotificationAudience` field (default `Admins`) to the `SiteConfig` record (`src/Piro.Application/Interfaces/ISiteConfigRepository.cs:12-21`). Stored as a `SiteData` row (`DataType="string"`), read/written through the existing `ISiteConfigRepository.GetAsync`/`SetAsync`.

New DbSet on `PiroDbContext` (`src/Piro.Infrastructure/Persistence/PiroDbContext.cs:18-53`): `SystemNotifications`; its `IEntityTypeConfiguration` auto-discovered (`:59`).

New DI registrations (`src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs`): the singleton `Channel<ISystemEvent>` (unbounded, mirroring the `Channel<CheckStatusChangedEvent>` registration at `:129`); `SystemEventDrainHostedService` as an `IHostedService` (mirroring `StatusDrainHostedService`); each `ISystemEventHandler<TEvent>` (as an `IEnumerable`, per the dispatcher-collection convention at `:204-214`); `ISystemNotificationChannel → EmailSystemChannel`; `ISystemNotificationService → SystemNotificationService`; and the global `IJobListener` for the exception bridge (§4.6) added to the Quartz configuration at `:133-163`.

**No changes to:** `Alert`/`AlertConfig`/`AlertNotificationContext`; any dispatcher; `EscalationCheckerService`/`AlertLifecycleService`/`AlertEvaluationService`; `AppRole`/`AppUser`/Identity schema; `ServiceStatus`/`AlertSource`/`IntegrationType`/`PersonalNotificationChannel` enums.

## 6. Phased plan

Each phase is independently shippable.

1. **Core pipeline + persistence + event transport.** `SystemNotification` entity + enums + migration + EF config; the `Channel<ISystemEvent>` + `SystemEventDrainHostedService` (§4.1); `ISystemEventHandler<T>` + the v1 handlers (§4.2); `ISystemNotificationService` with dedup/persist/resolve (§4.3, §4.5); `ISystemNotificationChannel` + `EmailSystemChannel` (§4.4); the `Roles` constants and audience resolution (§4.7); the `SiteConfig` audience setting (§5). Delivered without any emitter wired — testable by publishing an event manually.
2. **Audience setting UI.** The single select on the admin Site-settings screen (§4.7). Small, ships once the setting exists.
3. **Wire `CheckUnschedulable`.** Publish `CheckUnschedulableEvent`/`CheckSchedulableAgainEvent` from the RFC 0008 Part B scheduler when the matching-worker set is empty / recovers (§4.4, §4.6). Depends on RFC 0008 Part B existing; until then this phase is a no-op stub.
4. **Wire `TagReconciliation` + the exception bridge.** Publish `SystemTagDriftedEvent` from the shared tag-reconciliation component (RFC 0008), and add the global `IJobListener` that publishes `BackgroundJobFailedEvent` on a crashed Quartz job (§4.6).
5. **In-app admin feed (optional, later).** A read/acknowledge UI over the persisted rows (`AcknowledgedAt`), turning the audit trail into a dashboard. Not required for delivery; the schema already anticipates it.

Phases 3–4 are the payoff for RFC 0008 but are gated on 0008's implementation; phases 1–2 stand alone and are useful for any future system event.

## 7. Alternatives considered

- **Model a system notification as an orphan `Alert` (reuse the alert pipeline).** Rejected — `AlertNotificationContext` is alert-coupled (`ServiceName`/`CheckName` mandatory non-nullable, `Severity` is `AlertSeverity`, templates render "Alert fired"/"Status"/"Severity", `src/Piro.Application/Models/AlertNotificationContext.cs:26-71` and `src/Piro.Infrastructure/Alerts/AlertMessageTemplates.cs`). Forcing "YAML import failed" through it leaks alert chrome, and the delivery path (`EscalationCheckerService`) is on-call-driven, which system notifications explicitly are not (§3). A parallel, neutral pipeline is smaller than bending the alert model.
- **Emitters call the delivery service directly (no event layer).** The original draft had the scheduler call `RaiseAsync(category, severity, title, body, …)` itself. Rejected — it forces the *detector* to know the *response* (severity, wording, that it should even notify), coupling every emitter to notification policy. The event → handler split (§4.1–4.2) lets the scheduler announce a fact and nothing more, so response policy can change (and later become data-driven) in one place without touching any emitter.
- **Exceptions as the transport.** Rejected — an unschedulable check does not abort the scheduler, so signalling it with `throw` is control-flow abuse: it would break the tick, need a domain-aware `catch` at a generic boundary, and turn the stack into a message bus. Domain events are the transport; a *real* caught exception is merely one *source* of an event, via the `IJobListener` bridge (§4.6).
- **A third-party mediator (MediatR) for the event bus.** Rejected — Piro already has an in-process event pattern (`Channel<T>` + a draining `BackgroundService`, `src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:129`, `src/Piro.Infrastructure/Jobs/StatusDrainHostedService.cs:19-38`). Reusing it keeps one event idiom in the codebase and adds no dependency; a mediator library would be a parallel mechanism for the same job.
- **A dynamic rules engine for policy in v1.** Rejected for v1 — configurable event→severity→channel rules are powerful but a feature in themselves. Handlers (§4.2) hardcode policy today and are the exact seam where a rules lookup slots in later, with no change to emitters or delivery. Ship the seam, not the engine.
- **Reuse `INotificationDispatcher.SendPersonalMessageAsync` directly for email.** Rejected for email specifically — `EmailDispatcher.SendPersonalMessageAsync` hardcodes the subject to the verification-code string (`src/Piro.Infrastructure/Alerts/EmailDispatcher.cs:28-34`). `EmailSystemChannel` over `IEmailService.SendAsync` gives subject control while keeping the *seam* that lets other channels reuse their `SendPersonalMessageAsync` later (§4.4).
- **Add `SendSystemNotificationAsync` to `IEmailService` (mirroring `SendInvitationAsync`).** Rejected — it would bake email into the pipeline's contract. Routing through `ISystemNotificationChannel` keeps the pipeline channel-agnostic; the email specifics live in `EmailSystemChannel`, one small class, not on the shared `IEmailService` interface.
- **Fire-and-forget (no persistence).** Rejected — dedup (§4.5) needs a durable record of what is currently active, or a recurring condition emails every tick. Persistence also enables the audit trail and the future admin feed for the cost of one table.
- **A configurable free-form recipient address (e.g. `ops@company.com`) instead of a role audience.** Rejected as the *primary* mechanism — it drifts (person leaves, address rots) and duplicates the user directory Piro already has. Role-set audience is dynamic and always non-empty (§4.7). An optional extra-destinations list *on top of* the role audience is a reasonable future addition but is not needed for v1.
- **Introduce a `Level`/`Rank` on `AppRole` to express "admin and above" generically.** Rejected for this RFC — it is a larger, cross-cutting change to the roles model with its own blast radius (custom roles would need a defined rank), for no benefit v1 needs. Three named sets (§4.7) cover the requirement without touching the role schema; a rank can come later if custom-role ordering is ever required.

## 8. Risks

- **Email is the only channel and can itself be the thing that's broken.** If the operational event *is* "SMTP is misconfigured," the email system notification about it cannot be delivered — the classic "who watches the watcher" gap. Mitigation: the notification is still *persisted* (§4.4), so a future in-app admin feed (§6, phase 5) or a second channel (§4.4) surfaces it even when email is down. v1 accepts this gap for email-configured instances; it is strictly better than today's silence.
- **Audience misconfiguration silently narrows recipients.** Setting the audience to `Owners` on an instance whose owner's mailbox is stale means near-silent delivery. Mitigation: default is `Admins` (broader), the audience is never empty (§4.7), and the persisted row is the backstop. The setting's helper text should name the concrete roles so the operator sees the blast radius.
- **Fingerprint too coarse folds distinct events; too fine spams.** If `CheckUnschedulable` fingerprinted on a constant, two different unschedulable checks would collapse into one notification; if it included a timestamp, every tick would be "new" and email every minute. Mitigation: fingerprint on the stable identity of the condition (the check id, the entity+key) — the same discipline `Alert.MessageFingerprint` uses (§4.5). The handler owns the fingerprint (§4.2), so this is chosen deliberately per event type in one place.
- **A never-resolved notification lingers active forever.** If a condition clears without an inverse event ever being published, the row stays `ResolvedAt == null` and future recurrences fold into it silently rather than re-notifying. Mitigation: pair every raising event with its inverse (e.g. `CheckUnschedulableEvent` ↔ `CheckSchedulableAgainEvent`, §4.1) and its resolving handler, exactly as the alert path pairs record/resolve; treat an un-paired emitter as a code-review defect.
- **Unbounded channel backpressure.** The `Channel<ISystemEvent>` is unbounded (matching the existing `CheckStatusChangedEvent` channel); a storm of events (every check unschedulable at once during a mass worker outage) enqueues many items. Mitigation: dedup happens on the *drain* side (§4.5), so a storm of the same condition collapses to one delivery regardless of enqueue count — the queue drains fast because most events no-op at the dedup step. Distinct conditions are naturally bounded by the number of checks/entities.
- **Coupling to RFC 0008 for the only two v1 events.** Both wired events (§4.4) depend on RFC 0008 Part B / its reconciliation component. If 0008 slips, phases 3–4 have nothing to publish. Mitigation: phases 1–2 are independent and deliver the reusable framework (the `BackgroundJobFailedEvent` exception bridge, §4.6, works with zero 0008 dependency and gives the framework immediate standalone value); the emitters are thin and land with 0008.
