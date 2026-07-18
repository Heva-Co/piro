# RFC 0009 — Notification system revamp: delivery contracts, a push engine for non-paging notifications, and group broadcast

Status: proposal
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-18

## 1. Problem

Every outbound notification in Piro is forced through one interface shaped for one job, and the whole notification surface is bent around that mismatch. `INotificationDispatcher` (`src/Piro.Application/Interfaces/INotificationDispatcher.cs:8-28`) has exactly two methods and both address *one person's* handle:

```csharp
IntegrationType Type { get; }                                                       // :10
Task<bool> DispatchPersonalAsync(Integration? integration, string handle,
    AlertNotificationContext context, CancellationToken ct = default);              // :19
Task<bool> SendPersonalMessageAsync(Integration? integration, string handle,
    string message, CancellationToken ct = default);                               // :27
```

Four concrete problems follow from that single shape.

**1. Six dispatchers are dead stubs because the personal mould does not fit them.** `SlackDispatcher`, `DiscordDispatcher`, `MsTeamsDispatcher`, `GoogleChatDispatcher`, `OpsgenieDispatcher`, and `WebhookDispatcher` (all in `src/Piro.Infrastructure/Alerts/`) implement both methods as `Task.FromResult(false)` — e.g. `SlackDispatcher.cs:15-19`. Their `IntegrationType` values are `[Obsolete("Not supported for now.")]` (`src/Piro.Domain/Enums/IntegrationType.cs`: `Webhook=3` :49-50, `Slack=4` :56-57, `MSTeams=7` :84-85, `GoogleChat=10` :113-114, `Discord=11` :120-121), and none is registered (`InfrastructureServiceExtensions.cs:210-213` registers only Email, Telegram, Twilio, Ntfy). They return `false` not because they are unfinished but because they *cannot honestly implement a personal-handle method*: Slack, Discord, MS Teams, and Google Chat post to a **shared channel a whole team watches**, not to one individual. The `false` is the interface admitting a type mismatch.

**2. Notification dispatch is polling-only, and the one event pipeline that exists is disconnected from it.** The only code that ever sends a notification is `EscalationCheckerService`, driven by a Quartz cron job firing every minute (`EscalationCheckJob`, cron `0 * * * * ?`, `InfrastructureServiceExtensions.cs:157-161`) that scans active alerts and pages on-call users (`EscalationCheckerService.cs:180`). Separately, a `Channel<CheckStatusChangedEvent>` (`InfrastructureServiceExtensions.cs:129`) drained by `StatusDrainHostedService` (`src/Piro.Infrastructure/Jobs/StatusDrainHostedService.cs:19-38`) exists — but it only recomputes public service status; it never touches a notification. So the seam between "a check went DOWN and an `Alert` row was written" (inline, `AlertLifecycleService.RecordOccurrenceAsync`, `src/Piro.Application/Services/AlertLifecycleService.cs:25-67`) and "someone is told" is a one-minute polling gap, and there is no event-driven, durable, retrying path for any notification that is *not* escalated paging.

**3. There is no way to notify a team channel, independent of on-call.** A team that wants their Google Chat to know a service is down — *regardless of who is paged* — has no path. `EscalationCheckerService` walks a service's `EscalationPolicy` to on-call users' personal handles (`:158-196`) with an explicit "No fallback to any global channel" (`:158-160`). Group awareness is not a missing dispatcher; it is a missing *delivery mode* the interface cannot express and a missing *source* to drive it.

**4. Piro cannot tell an administrator that Piro itself is broken.** Every notification originates from an `Alert` about *a monitored service*. There is no notion of an operational event about the platform (a check that matches no worker, a crashed background job, internal tag drift), and no audience of administrators to receive one. RFC 0008 walked straight into this: two of its risks (0008 §8) are only mitigated if Piro can email an admin out-of-band from service on-call, and that mechanism does not exist.

A fifth, smaller strain ties them together: `SendPersonalMessageAsync` is onboarding, not alerting — its sole caller delivers a one-time verification code (`src/Piro.Application/Services/UserManagementService.cs:369-370`) — yet it rides the alert interface, which is why every stub carries a *second* no-op method and `Pushover` is a half-stub (`DispatchPersonalAsync` works, `PushoverDispatcher.cs:20`; `SendPersonalMessageAsync` is stubbed, `:32-33`).

This RFC revamps the notification system to fix all four, in one coherent model with a single principle behind every choice: **separate the delivery contract (how a message physically leaves) from the delivery engine (how a notification is scheduled, retried, and routed), and keep on-call paging — which is correctly a timed, polled, stateful process — exactly as it is.** It supersedes the narrower "system notifications to administrators" draft this RFC replaces (issue #187, this same file), preserving that design's core — event→handler→delivery separation, persisted dedup, role-based audience, the exception bridge — while widening it into the general notification engine those pieces were really describing.

## 2. Non-goals

- **Re-architecting on-call paging.** Paging is intrinsically temporal — "notify step 1, wait 5 minutes, if still active escalate to step 2" — which is exactly what a polled Quartz job models well and what an event bus models badly (it would have to re-schedule future events per delay, reinventing a stateful scheduler). `EscalationCheckerService`'s *behavior* — on-call resolution, per-step timing (RFC 0006), priority-ordered "first working preference wins" (`EscalationCheckerService.cs:171-196`) — is unchanged. It is only recompiled against the renamed personal interface (§4.9); paging does **not** move onto the push engine.
- **Designing the stateful paging hand-off (mode 3).** PagerDuty/Opsgenie — the `trigger`/`resolve`-with-dedup-key contract where Piro tells another platform *what happened* and that platform owns the rotation — are RFC 0004's territory (`IAlertEventDispatcher`, `ServiceIntegrationMapping`). This RFC names that mode to draw the boundary (§4.2) and neither designs nor folds it in.
- **Reinventing tags.** The tag model and selector grammar are RFC 0008's (`docs/rfcs/0008-service-check-worker-tags.md` §4.1, §4.5). Broadcast *consumes* them; it does not define `Tag`, the join tables, or the selector shape. The tag-filter phase is gated on RFC 0008 Part A landing (§8).
- **Per-service broadcast configuration.** Attaching channels to each `Service` does not scale to hundreds of services (§4.6, §7). Broadcast is centralized subscriptions matched against every alert.
- **A public-facing subscription surface.** End-user "subscribe to incident updates" (issues #8, #54) is per-visitor and public; it stays separate from these operator-facing team broadcasts. System notifications never appear on `apps/web` and never affect `ServiceStatus`/`PublicStatus`.
- **A dynamic, admin-authored routing-rules engine.** Handlers (§4.4) hardcode response policy (which severity, which channels) in v1. Broadcast filters on two fixed axes (severity + tag selector). A general event→channel rules DSL is out of scope; the handler and the subscription row are the seams it would grow from.
- **Multi-content generalization beyond what ships.** The delivery interfaces are generic over content type (§4.3), but v1 defines exactly two contents (`AlertNotificationContext`, `SystemNotificationContent`). Incidents and maintenance are **not** wired through dispatchers (they surface on the status page and admin API only, `IncidentAppService.cs:11-14` has no dispatcher dependency) and are not modeled here.

## 3. Design principle

**Two axes, kept orthogonal: *what a notification is about* (content) and *how it physically leaves* (delivery contract) vary independently, and neither is entangled with *how a notification is scheduled and routed* (the engine).** A dispatcher knows a channel and renders a concrete content type; it knows nothing about alerts-vs-system policy, retries, or audience. An engine decides when and to whom; it knows nothing about Slack's payload format. Paging is the one flow that legitimately needs timed, stateful scheduling, so it keeps its own polled engine; everything else — team broadcast, admin system notifications — is fire-and-forget and shares one durable push engine. Every decision below traces to this split.

## 4. Design

```
   A CHECK FAILS / RECOVERS
          │
   CheckResultIngesterService.IngestAsync (inline, unchanged)
          │
   AlertLifecycleService.RecordOccurrenceAsync  — creates/resolves the Alert row (unchanged)
          │
          ├───────────────────────────────────────────────┐
          ▼ [unchanged]                                     ▼ [NEW — the one integration point on the alert path]
   (paging path continues on its own)          INotificationEventPublisher.PublishAsync(AlertOpenedEvent / AlertResolvedEvent)
                                                             │  → INSERT NotificationOutbox (Pending)
  ─────────────────────────────────────────────  ──────────┼─────────────────────────────────────────────
   PAGING — Quartz / pull (unchanged behavior)   │   PUSH ENGINE — durable outbox (NEW)
  ─────────────────────────────────────────────  │  ─────────────────────────────────────────────────────
   EscalationCheckJob (every 1 min)              │   NotificationDispatchWorker : BackgroundService
     → EscalationCheckerService                  │     drains Pending outbox rows, retry + backoff
     → on-call users, per-step timing            │     → resolves INotificationEventHandler<TEvent>(s)
     → IPersonalNotificationDispatcher            │            │
        <AlertNotificationContext>.SendAsync      │            ├─► BroadcastHandler
        (renamed from DispatchPersonalAsync)      │            │     match enabled BroadcastSubscriptions
     (to the ON-CALL PERSON)                      │            │     (MinSeverity + RFC-0008 tag selector)
                                                  │            │     → IGroupNotificationDispatcher
                                                  │            │        <AlertNotificationContext>.SendAsync
                                                  │            │        (to TEAM CHANNELS)
                                                  │            │
                                                  │            └─► SystemNotificationHandler
   also feeding the push engine (system events):  │                  dedup (Category,Fingerprint); role audience
     CheckUnschedulableEvent  (RFC 0008 sched.) ─┐│                  → ISystemNotificationChannel (facade)
     BackgroundJobFailedEvent (Quartz listener) ─┼┤                     └ delegates → IPersonalNotificationDispatcher
     SystemTagDriftedEvent    (RFC 0008 reconc.) ┘│                                   <SystemNotificationContent>.SendAsync
                                                  │                                   (to ADMINS: Email/SMS)
  ─────────────────────────────────────────────  ─────────────────────────────────────────────────────────
   VERIFICATION — synchronous, NOT the engine:  UserManagementService → IVerificationCodeSender.SendCodeAsync
   mode 3 — NOT here (RFC 0004):                 ISystemEventDispatcher.TriggerAsync / ResolveAsync (PagerDuty, Opsgenie)
```

### 4.1 The three delivery contracts

The interface split names the three contracts today's single `INotificationDispatcher` conflates. Two live here; the third is RFC 0004's, listed only to fix the boundary.

| Mode | Contract | Target | Lifecycle | Interface | Owner |
|---|---|---|---|---|---|
| **1. Personal** | Reach one person | Their own handle (chat id, phone, email) | Fire-and-forget | `IPersonalNotificationDispatcher<TContent>` | this RFC |
| **2. Group** | Post to a shared team space | A channel/room/topic (not a person) | Fire-and-forget | `IGroupNotificationDispatcher<TContent>` | this RFC |
| **3. System event** | Hand off to a paging platform | A routing key / service | `trigger`/`resolve`, dedup key | `ISystemEventDispatcher` (RFC 0004) | RFC 0004 |

Per-provider fit (the reason mode 1 and mode 2 must be *separate* interfaces, not one method with a mode flag):

| Provider | `IntegrationType` | Personal | Group | Notes |
|---|---|---|---|---|
| Email | `Email` (:32-43) | ✅ | — | Group DL deferred (§4.6) |
| Twilio SMS | `Twilio` (:98-107) | ✅ | — | A phone number is one person |
| Pushover | `Pushover` (:136-137) | ✅ | — | User key = one person's devices |
| Telegram | `Telegram` (:87-96) | ✅ | ✅ | Same `chat_id` mechanism: private chat *or* group |
| Ntfy | `Ntfy` (:139-148) | ✅ | ✅ | A topic can be private or team-shared |
| Slack | `Slack` (:56-57) | — | ✅ | Incoming webhook → a channel |
| Google Chat | `GoogleChat` (:113-114) | — | ✅ | Webhook → a space |
| Discord | `Discord` (:120-121) | — | ✅ | Webhook → a channel |
| MS Teams | `MSTeams` (:84-85) | — | ✅ | Webhook → a channel |
| Webhook | `Webhook` (:49-50) | — | ✅ | Generic POST (not-a-person) |
| PagerDuty / Opsgenie | (:64-65, :128-129) | — | — | Mode 3 — RFC 0004, not here |

`Telegram` and `Ntfy` implementing *both* is precisely what the single interface could not express: today a Telegram group and a Telegram DM would both squeeze through `DispatchPersonalAsync`, with no way for the caller to say which it means.

### 4.2 Content, and why the channel (not the content) renders

There are two axes that vary — *channel* × *content* — and the knowledge "how does content X look on channel Y" must live in exactly one place. It lives on the **channel**: a dispatcher receives a concrete content type and renders it, reusing the existing per-channel template infrastructure. This is what Piro already does — `AlertMessageTemplates` (`src/Piro.Infrastructure/Alerts/AlertMessageTemplates.cs:10-16`) already centralizes each channel's alert wording (Markdown for Telegram, HTML for Email, plain text for SMS) as Scriban templates, "centralizing what used to be scattered as string-interpolation in each dispatcher." The revamp keeps that; it does *not* introduce a content-renders-itself model (which would force `AlertNotificationContext`, an `Application` type, to know every channel's format, an `Infrastructure` concern).

Content types are plain records under a marker interface used only as a generic constraint — it carries no `RenderFor`, so it re-introduces no channel↔content coupling:

```csharp
// src/Piro.Application/Models/INotificationContent.cs
public interface INotificationContent { }

public record AlertNotificationContext(...) : INotificationContent;   // existing (AlertNotificationContext.cs:26-71), now marked
public record SystemNotificationContent(                              // NEW — the neutral, non-alert content
    SystemNotificationCategory Category,
    SystemNotificationSeverity Severity,
    string Title,
    string Body) : INotificationContent;
```

Adding a content type is a new method per channel that must carry it (the accepted cost of channel-renders-content). It is **not** a change to the interfaces or the engine.

### 4.3 The two notification interfaces (generic over content)

`INotificationDispatcher` is replaced by two interfaces, split on target shape and generic over content:

```csharp
// src/Piro.Application/Interfaces/IPersonalNotificationDispatcher.cs
public interface IPersonalNotificationDispatcher<in TContent> where TContent : INotificationContent
{
    IntegrationType Type { get; }
    // integration is null for self-sufficient channels (Email); handle identifies the person.
    Task<bool> SendAsync(Integration? integration, string handle, TContent content, CancellationToken ct = default);
}

// src/Piro.Application/Interfaces/IGroupNotificationDispatcher.cs
public interface IGroupNotificationDispatcher<in TContent> where TContent : INotificationContent
{
    IntegrationType Type { get; }
    // integration always required (holds the channel credentials); target = the room/space/topic,
    // null when the integration's ConfigJson already fully identifies the destination (e.g. a
    // Slack incoming-webhook URL that is itself channel-specific).
    Task<bool> SendAsync(Integration integration, string? target, TContent content, CancellationToken ct = default);
}
```

A concrete dispatcher implements one interface instantiation per (mode × content) it supports — one class per provider, sharing that provider's HTTP client and config:

```csharp
internal class EmailDispatcher :
    IPersonalNotificationDispatcher<AlertNotificationContext>,
    IPersonalNotificationDispatcher<SystemNotificationContent>,
    IVerificationCodeSender
{
    public IntegrationType Type => IntegrationType.Email;
    public Task<bool> SendAsync(Integration? i, string h, AlertNotificationContext c, CancellationToken ct)  // AlertMessageTemplates
    public Task<bool> SendAsync(Integration? i, string h, SystemNotificationContent c, CancellationToken ct) // SystemMessageTemplates (§4.7)
    public Task<bool> SendCodeAsync(...)                                                                     // raw string
}

internal class SlackDispatcher : IGroupNotificationDispatcher<AlertNotificationContext> { ... }   // group-only
internal class TwilioSmsDispatcher :
    IPersonalNotificationDispatcher<AlertNotificationContext>,
    IPersonalNotificationDispatcher<SystemNotificationContent>,
    IVerificationCodeSender { ... }                                                                // personal-only, all contents
```

**Why generic, not one method per content on a non-generic interface.** A dispatcher that cannot carry a content simply does not implement that instantiation — so "Slack cannot send a personal system notification" is a *type-system* fact (`SlackDispatcher` has no `IPersonalNotificationDispatcher<SystemNotificationContent>`), resolvable at wiring time, not a runtime `false`. Resolution becomes per-(content×mode): the paging path resolves `IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>>`; the system handler resolves `IEnumerable<IPersonalNotificationDispatcher<SystemNotificationContent>>`; the broadcast handler resolves `IEnumerable<IGroupNotificationDispatcher<AlertNotificationContext>>` — each into the same `Dictionary<IntegrationType, …>` shape `EscalationCheckerService` already builds (`:25-26`).

The `bool` return is kept: the paging path uses it to fall through to a user's next preference (`EscalationCheckerService.cs:180-181`); the group/system handlers use it to log a per-target failure without aborting the fan-out.

### 4.4 The push engine: events, durable outbox, handlers

Everything that is *not* paging is fire-and-forget with no per-step timing: a team broadcast, an admin system notification. These share one durable, retrying push engine. Its three layers keep the detector from deciding the response (the principle the superseded draft established and this RFC keeps).

**Events.** A source publishes a fact and nothing more:

```csharp
// src/Piro.Application/Models/NotificationEvents/INotificationEvent.cs
public interface INotificationEvent { }

public record AlertOpenedEvent(int AlertId)   : INotificationEvent;   // → broadcast
public record AlertResolvedEvent(int AlertId) : INotificationEvent;   // → broadcast
public record CheckUnschedulableEvent(int CheckId, string SelectorSummary) : INotificationEvent;  // → system
public record CheckSchedulableAgainEvent(int CheckId) : INotificationEvent;                        // → system (inverse)
public record BackgroundJobFailedEvent(string JobKey, string Error)         : INotificationEvent;  // → system
public record SystemTagDriftedEvent(string Entity, string Key)              : INotificationEvent;  // → system
```

**Durable outbox — the transport.** The superseded draft used an in-memory `Channel<ISystemEvent>` and named "unbounded channel backpressure" plus crash-loss as accepted risks. This RFC upgrades the transport to a persistent outbox, because a notification saying "your service is down" must survive a process restart, and "with retries" is not honestly deliverable on an in-memory queue (a crash between dequeue and send loses the event). The publisher writes a row; a worker drains it:

```csharp
// src/Piro.Domain/Entities/NotificationOutbox.cs
public class NotificationOutbox
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;   // discriminator → the INotificationEvent record type
    public string PayloadJson { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }                // Pending | Processing | Done | Failed
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }      // backoff schedule
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
public enum OutboxStatus { Pending, Processing, Done, Failed }

// src/Piro.Application/Interfaces/INotificationEventPublisher.cs
public interface INotificationEventPublisher
{
    Task PublishAsync(INotificationEvent evt, CancellationToken ct = default);   // serializes → INSERT Pending
}
```

`NotificationDispatchWorker : BackgroundService` (new, modeled on `StatusDrainHostedService.cs:19-38`) polls Pending rows whose `NextAttemptAt` has arrived, marks them `Processing`, deserializes to the event record, resolves the handler(s), runs them, and marks `Done` — or, on throw, increments `Attempts`, records `LastError`, and reschedules `NextAttemptAt` with exponential backoff until a cap, after which the row is `Failed`. This is a short polling loop like paging's, but it is the engine's *plumbing*, not paging's *domain timing*: it fires on every enqueue (near-immediate), not on a fixed operational tick, and it carries retry/durability that the paging scan gets for free from re-reading the DB.

**Handlers — where response policy lives.** One handler per event type decides severity, audience, wording; registered as `IEnumerable<INotificationEventHandler<TEvent>>` (the DI-collection pattern already used for dispatchers, `InfrastructureServiceExtensions.cs:204-214`):

```csharp
public interface INotificationEventHandler<in TEvent> where TEvent : INotificationEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}
```

- `BroadcastHandler : INotificationEventHandler<AlertOpenedEvent>` (and `…<AlertResolvedEvent>`) — loads the `Alert`, builds its `AlertNotificationContext` (reusing `Alert.ToNotificationContext`, `src/Piro.Application/Extensions/AlertExtensions.cs:41-59`), matches broadcast subscriptions (§4.6), and dispatches to each matching channel.
- `SystemNotificationHandler` per system event — decides `Severity`/`Category`/wording (hardcoded policy in v1; the seam for a future rules table), then calls the system delivery service (§4.7).

### 4.5 Broadcast subscriptions — centralized, filter-matched

The motivating requirement: a team's channel must learn a monitored service is down *regardless of who is on call*, and it must stay manageable across hundreds of services. Central subscriptions matched against every alert (not a field on `Service`, §7) mean a new service is auto-covered by existing rules with zero per-service setup.

```csharp
// src/Piro.Domain/Entities/BroadcastSubscription.cs
public class BroadcastSubscription
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;     // "Ops team – Google Chat"

    public Guid ChannelIntegrationId { get; set; }       // FK → Integration (Guid PK, Integration.cs:7)
    public Integration ChannelIntegration { get; set; } = null!;
    public string? Target { get; set; }                  // room/space/topic; null if the integration self-addresses

    public AlertSeverity MinSeverity { get; set; }       // gate 1: fires at this severity or worse
    public string? TagSelectorJson { get; set; }         // gate 2: RFC 0008 §4.5 selector vs the Service's effective tags

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

`BroadcastHandler` evaluates every enabled subscription against an alert: (1) the alert's `Severity` (`AlertNotificationContext.cs:36`) must be ≥ `MinSeverity`; (2) if `TagSelectorJson` is set, it is deserialized to RFC 0008's selector (`anyOf` of `allOf` groups; operators `=`, `In`, `NotIn`, `Exists`; `0008` §4.5) and matched against the alert's `Service`'s effective tags (`0008` §4.3 — inheritance flows service→check only, so for a service its effective tags are its own). A null selector matches every service; a subscription with neither filter is "this channel gets everything." A handful of rows covers hundreds of services, present and future.

### 4.6 Admin UI

Two surfaces in `apps/admin`, both extending existing screens.

**(a) Broadcast subscriptions — new, `apps/admin/src/features/broadcast/`** (shaped like `features/escalation/`): a `BroadcastSubscriptionsPage.tsx` list (mirroring `EscalationPoliciesPage.tsx`) and a `BroadcastSubscriptionForm.tsx` editor. Fields:
- **Channel** — a select over Integrations whose manifest has `SendsGroupNotification` (§4.8), reusing the capability-filtered picker logic of `IntegrationTypeGrid.tsx` but over existing Integration instances.
- **Target** — free-text room/space/topic, shown only for types that need one (Telegram group id, Ntfy topic); hidden for self-addressing webhook types.
- **Minimum severity** — a select over `AlertSeverity`.
- **Tag selector** — reuses whatever RFC 0008 ships for selector authoring (§8 dependency); until then the field is hidden and the subscription behaves as "all services."

One component per file (`AGENTS.md`), `export default` at the bottom, `props: Props` destructured in the body.

**(b) System-notification audience — one select on the existing Site-settings screen** (backed by `SiteController`, `src/Piro.Api/Controllers/SiteController.cs`): "Send system notifications to" → `Owners` / `Admins` (default) / `Members`, with helper text naming the roles each covers (§4.7). A new field in the settings form, not a new page.

**(c) Personal preferences + integration picker — unchanged behavior, filtered by the new flag.** `NotificationPreferenceRow.tsx` (`apps/admin/src/components/notification-preferences/`) and the integration picker branch on `SendsPersonalNotification` vs `SendsGroupNotification` to decide which types appear where — a predicate change, no new UI state. The send-code/confirm-code verification flow (`handleSendCode`/`handleConfirmCode`) is untouched (it calls `IVerificationCodeSender` now, §4.9, but the screen is identical).

### 4.7 System notifications: persistence, dedup, audience, delivery

The superseded draft's core is kept intact; only its delivery tail changes.

**Persisted, deduped — unchanged from the draft.** A `SystemNotification` row (`Id`, `Category`, `Severity`, `Title`, `Body`, `Fingerprint`, `FiredAt`, `ResolvedAt?`, `OccurrenceCount`, `AcknowledgedAt?`) records each condition; a partial index on `(Category, Fingerprint) WHERE ResolvedAt IS NULL` backs dedup. `RaiseAsync` folds a recurring `(Category, Fingerprint)` into `OccurrenceCount++` without re-sending, mirroring `AlertLifecycleService`'s strategy (`AlertLifecycleService.cs:35-43`); the inverse event resolves the row. It has no `CheckId`/`ServiceId`/`EscalationPolicyId` — a system notification is about the platform, never joins the service-status graph, and any entity reference lives in `Body`/`Title`. Categories v1: `CheckUnschedulable`, `TagReconciliation` (extensible); severities `Info`/`Warning`/`Critical`.

**Audience by named role sets — unchanged from the draft.** Roles are flat (`AppRole : IdentityRole<int>`, `src/Piro.Domain/Entities/AppRole.cs:10`, no rank), so the audience is one of three explicit sets — `Owners` = {Owner}, `Admins` (default) = {Owner, Admin}, `Members` = {Owner, Admin, Member} — resolved by unioning `UserManager.GetUsersInRoleAsync(...)` (the API already used at `SetupController.cs:239`), stored on `SiteConfig`. The audience is never empty: the last Owner cannot be deleted or demoted (`UserManagementService.cs:174-178`).

**Delivery — changed: the facade now delegates to the generic dispatcher.** The draft delivered via `ISystemNotificationChannel → EmailSystemChannel → IEmailService`, deliberately *bypassing* the dispatchers because the old `EmailDispatcher.SendPersonalMessageAsync` hardcoded the subject to the verification-code string and gave no subject control. The revamp removes that obstacle: dispatchers are now generic over content, so `EmailDispatcher` renders `SystemNotificationContent` through its own `SystemMessageTemplates` with full subject/body control. So `ISystemNotificationChannel` **is kept as a facade** (the draft's clean seam, and the place the "email-only in v1" and role-audience logic lives), but its implementation now **delegates** to `IPersonalNotificationDispatcher<SystemNotificationContent>` resolved by the recipient's channel, instead of wrapping `IEmailService` directly:

```csharp
// src/Piro.Infrastructure/Notifications/DispatcherBackedSystemChannel.cs
internal class DispatcherBackedSystemChannel(
    IEnumerable<IPersonalNotificationDispatcher<SystemNotificationContent>> dispatchers) : ISystemNotificationChannel
{
    // resolves the dispatcher for the recipient's channel type and calls SendAsync(..., content, ct)
}
```

This is why the delegation is possible *now* and was not in the draft: the generic-content revamp (§4.3) is the enabling change. v1 still delivers system notifications over Email/SMS only (the personal channels that carry `SystemNotificationContent`); a group system notification (e.g. to an ops channel) is a later addition of `IGroupNotificationDispatcher<SystemNotificationContent>` with no pipeline change.

**Event sources — unchanged from the draft, now onto the outbox.** Explicit emitters (RFC 0008 Part B scheduler → `CheckUnschedulableEvent`/`CheckSchedulableAgainEvent`; the tag reconciler → `SystemTagDriftedEvent`) publish via `INotificationEventPublisher`. The **exception bridge** is kept: a global Quartz `IJobListener` (added to the config at `InfrastructureServiceExtensions.cs:133-163`) inspects `JobExecutionException` in `JobWasExecuted` and publishes `BackgroundJobFailedEvent` — a *caught, already-failed* exception translated into an event, never `throw` as the signalling mechanism.

### 4.8 The manifest declares the mode

Which delivery contract a type honors becomes a manifest fact, so UI and runtime read it instead of hardcoding lists. `IntegrationCapability` (`src/Piro.Domain/Enums/IntegrationCapability.cs:9-28`) is `[Flags]`; its highest bit is `SupportsCheckCorrelation = 1 << 4` (`:27`). Add the next:

```csharp
SendsPersonalNotification = 1 << 0,   // existing (:15) — has a registered IPersonalNotificationDispatcher<>
SendsGroupNotification    = 1 << 5,   // NEW           — has a registered IGroupNotificationDispatcher<>
```

Manifests updated: keep `SendsPersonalNotification` on Email/Twilio/Ntfy/Telegram, **add** `SendsGroupNotification` to Telegram and Ntfy; drop `[Obsolete]` and add full `[IntegrationManifest(... SendsGroupNotification ...)]` to Slack/GoogleChat/Discord/MSTeams/Webhook; PagerDuty/Opsgenie stay `[Obsolete]` here (RFC 0004 gives them a system-event capability). The flag is what the UI branches on (personal-preference editor vs broadcast-subscription editor, §4.6). It flows through the existing `manifest.Capabilities.HasFlag(...)` reads (`IntegrationAppService.cs:62,100`) and `IntegrationManifestExtensions.CapabilityNames` (`src/Piro.Application/Extensions/IntegrationManifestExtensions.cs:44-48`) with no new plumbing.

### 4.9 Verification codes leave the alert interface

`SendPersonalMessageAsync` moves off the dispatchers into its own interface — sending a one-time code is transactional onboarding, not alerting, and stays **synchronous** (the user is waiting for the code; it does not go through the async outbox):

```csharp
// src/Piro.Application/Interfaces/IVerificationCodeSender.cs
public interface IVerificationCodeSender
{
    IntegrationType Type { get; }
    Task<bool> SendCodeAsync(Integration? integration, string handle, string code, CancellationToken ct = default);
}
```

Personal channels that send plain text (Email, Telegram, Twilio, Ntfy) implement it. `UserManagementService.SendNotificationPreferenceCodeAsync` (`UserManagementService.cs:354-373`) resolves from a `Dictionary<IntegrationType, IVerificationCodeSender>` — a one-line change to its lookup (`:363`) — and its existing "Channel does not support verification" guard (`:364`) now means "no `IVerificationCodeSender` registered," which is exactly right: a group-only type can never verify a personal handle, and the type system says so.

### 4.10 What does NOT change

- **Paging behavior.** `EscalationCheckerService`'s on-call resolution, per-step timing (RFC 0006), priority-ordered "first working preference wins," and "no fallback to a global channel" (`EscalationCheckerService.cs:158-196`) are unchanged. Paging stays polled/Quartz; it does not move onto the push engine.
- **The alert lifecycle/evaluation** (`AlertEvaluationService`, `AlertLifecycleService`, `AlertConfig`, thresholds). The push engine reads the *outcome* — the one addition is a single `PublishAsync` at the point the `Alert` is created/resolved.
- **`AlertNotificationContext`** — reused verbatim, now just marked `: INotificationContent`; no field changes.
- **The `Channel<CheckStatusChangedEvent>` status pipeline** (`InfrastructureServiceExtensions.cs:129`, `StatusDrainHostedService`) — untouched; it recomputes public status and is unrelated to notifications.
- **`UserNotificationPreference`/`PersonalNotificationChannel`, on-call/escalation model, `IncidentAppService`, the public status page, `ServiceStatus`/`PublicStatus`.**
- **Mode 3** (PagerDuty/Opsgenie) — left entirely to RFC 0004.

### 4.11 Interfaces that are renamed/recompiled when `INotificationDispatcher` is deleted

Deleting the old interface is not behavior-neutral at the call sites, even though behavior is preserved — two consumers recompile against the new interfaces:

- `EscalationCheckerService`: `Dictionary<IntegrationType, INotificationDispatcher>` → `<…, IPersonalNotificationDispatcher<AlertNotificationContext>>`; `dispatcher.DispatchPersonalAsync(...)` → `dispatcher.SendAsync(...)`; injected `IEnumerable<INotificationDispatcher>` → `IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>>` (`:20,25-26,180`).
- `UserManagementService`: resolves `IVerificationCodeSender` instead (§4.9).

Both are mechanical renames — no logic change — and are the whole of phase 1.

## 5. Data / schema scope

New tables (one migration, auto-applied on startup — `db.Database.Migrate()`, `src/Piro.Api/Program.cs:205`):

- **`NotificationOutbox`** — `Id` (bigint PK); `EventType`, `PayloadJson`, `Status` (enum-as-string), `Attempts`, `NextAttemptAt?`, `LastError?`, `CreatedAt`, `ProcessedAt?`. Index on `(Status, NextAttemptAt)` for the drain query.
- **`SystemNotifications`** — `Id` PK; `Category`, `Severity` (enum-as-string); `Title`, `Body`, `Fingerprint` (required); `FiredAt`, `ResolvedAt?`, `OccurrenceCount`, `AcknowledgedAt?`. Partial index on `(Category, Fingerprint) WHERE ResolvedAt IS NULL`.
- **`BroadcastSubscriptions`** — §4.5. FK `ChannelIntegrationId` → `Integration` (`Guid`), `ON DELETE CASCADE`.

New enum flag: `IntegrationCapability.SendsGroupNotification = 1 << 5` (§4.8) — a new high bit, no ordinal shift, existing persisted capability sets unaffected.

New enums (no schema): `OutboxStatus`; `SystemNotificationCategory` `{ CheckUnschedulable, TagReconciliation }`; `SystemNotificationSeverity` `{ Info, Warning, Critical }`; the `INotificationEvent` marker + event records; the `INotificationContent` marker.

New config key: `system:notification_audience` added to `SiteDataKeys.All` (`src/Piro.Application/Constants/SiteDataKeys.cs:16-26`) and a mapped `SystemNotificationAudience` field (default `Admins`) on the `SiteConfig` record (`src/Piro.Application/Interfaces/ISiteConfigRepository.cs:12-21`), read/written via the existing `ISiteConfigRepository`.

New DbSets on `PiroDbContext` (`src/Piro.Infrastructure/Persistence/PiroDbContext.cs:18-53`): `NotificationOutbox`, `SystemNotifications`, `BroadcastSubscriptions` (each with an auto-discovered `IEntityTypeConfiguration`, `:59`).

New DI (`InfrastructureServiceExtensions.cs`): the dispatchers re-registered under `IPersonalNotificationDispatcher<AlertNotificationContext>` / `<SystemNotificationContent>`, `IGroupNotificationDispatcher<AlertNotificationContext>`, and `IVerificationCodeSender` per their modes; `INotificationEventPublisher`; `NotificationDispatchWorker` as `IHostedService`; each `INotificationEventHandler<TEvent>` as an `IEnumerable`; `ISystemNotificationChannel → DispatcherBackedSystemChannel`; and the Quartz `IJobListener` (§4.7) at `:133-163`.

**Manifest edits (no schema):** un-obsolete + add `[IntegrationManifest]` to Slack/GoogleChat/Discord/MSTeams/Webhook; add `SendsGroupNotification` to Telegram/Ntfy. `IntegrationType` ordinals frozen — nothing reordered.

**Removed:** `INotificationDispatcher` (§4.11). **No changes to:** `Alert`/`AlertConfig`/`AlertNotificationContext` fields; `AppRole`/`AppUser`/Identity; `ServiceStatus`/`AlertSource`/`PersonalNotificationChannel`; the `Channel<CheckStatusChangedEvent>` pipeline.

## 6. Phased plan

Each phase is independently shippable.

1. **Interface split + verification extraction (pure refactor, no behavior change).** Introduce `INotificationContent`, `IPersonalNotificationDispatcher<TContent>`, `IGroupNotificationDispatcher<TContent>`, `IVerificationCodeSender`; migrate the four registered dispatchers + Pushover; recompile `EscalationCheckerService` and `UserManagementService` (§4.11); delete `INotificationDispatcher`. Covered by recompilation + existing tests.
2. **The push engine.** `NotificationOutbox` + `INotificationEventPublisher` + `NotificationDispatchWorker` (§4.4). Testable by publishing an event and asserting the outbox drains — no source wired yet.
3. **System notifications on the engine (supersedes the draft's delivery).** `SystemNotification` entity + dedup + role audience + `SystemNotificationContent` + `SystemMessageTemplates` + `DispatcherBackedSystemChannel` (§4.7); the Site-settings audience select (§4.6b); the exception-bridge `IJobListener` (works with zero RFC-0008 dependency — the framework's standalone value). This is the superseded draft's scope, re-landed on the new engine.
4. **Group dispatchers + manifest flag.** Implement `IGroupNotificationDispatcher<AlertNotificationContext>` for Slack/GoogleChat/Discord/MSTeams/Webhook (+ Telegram/Ntfy group paths); add `SendsGroupNotification`; un-obsolete and register. Group channels can now be dispatched to.
5. **Broadcast — severity only.** `BroadcastSubscription` + CRUD + `BroadcastHandler` on `AlertOpenedEvent`/`AlertResolvedEvent`, matching `MinSeverity`; admin UI (§4.6a) with the tag field hidden. Delivers the motivating use case end-to-end.
6. **Broadcast tag matching (gated on RFC 0008 Part A) + wired system events.** Enable `TagSelectorJson` and surface RFC 0008's selector editor; publish `CheckUnschedulableEvent`/`SystemTagDriftedEvent` from RFC 0008's scheduler/reconciler.
7. **In-app admin feed (optional).** A read/acknowledge UI over `SystemNotification.AcknowledgedAt`. Schema already anticipates it.

## 7. Alternatives considered

- **Keep one dispatcher interface, add a `DispatchGroupAsync` method.** Rejected — personal-only types (Twilio) would carry a no-op group method and group-only types (Slack) a no-op personal method; "does this dispatcher support this call" stays a runtime `bool`. Separate generic interfaces make "Slack cannot page a person" unrepresentable at wiring time.
- **Content renders itself (`INotificationContent.RenderFor(channel)`).** Rejected — it forces `AlertNotificationContext` (an `Application` type) to know every channel's format (an `Infrastructure` concern) and cross the layer boundary. The channel-renders-content model (§4.2) keeps rendering in `Infrastructure` where `AlertMessageTemplates` already lives; its cost (a method per channel per content) is accepted.
- **Fold PagerDuty/Opsgenie into the notification interfaces.** Rejected — mode 3 is a `trigger`/`resolve` lifecycle with a dedup key, not fire-and-forget; a shared interface would force a lifecycle onto plain channels or degrade PagerDuty to a stateless post. RFC 0004 owns it.
- **An in-memory `Channel<T>` for the push engine (the superseded draft's transport).** Rejected for the wider engine — a broadcast/system notification must survive a restart, and "with retries" is not deliverable on a queue that loses events on crash. The outbox (§4.4) is the durable form of the same event→drain idea, and the draft's own §8 listed crash-loss/backpressure as accepted risks that the outbox removes. It reuses no external mediator (no MediatR), keeping one event idiom.
- **Move paging onto the push engine too (one universal bus).** Rejected — escalation is inherently timed and stateful ("wait 5 min, if still active escalate"); on an event bus that means scheduling and cancelling future events per delay, reinventing the Quartz scheduler paging already uses correctly (RFC 0006). Paging stays polled; the engine handles only the timing-free flows.
- **Per-service broadcast channels (`Service.BroadcastChannels`).** Rejected — does not scale: hundreds of services means configuring the same "→ #ops" hundreds of times and remembering it on every new service. Central filter-matched subscriptions auto-cover present and future services (§4.5).
- **Broadcast as an `EscalationStep` type.** Rejected — a team channel is not an on-call target; it would inherit unwanted retry/ladder semantics and require the service to *have* an `EscalationPolicy` (often null, `Service.cs:51`). Broadcast must fire for policy-less services, so it lives parallel to escalation.
- **Model a system notification as an orphan `Alert`.** Rejected — `AlertNotificationContext` is alert-coupled (mandatory `ServiceName`/`CheckName`, `AlertSeverity`, "Alert fired" templates); forcing "YAML import failed" through it leaks alert chrome, and its delivery (`EscalationCheckerService`) is on-call-driven, which system notifications are not. `SystemNotificationContent` is the neutral content (§4.2).
- **Emitters call delivery directly (no event/handler split).** Rejected — it couples every detector to notification policy (severity, wording, whether to notify). The event→handler split lets a detector announce a fact and lets policy change (and later become data-driven) in one place.
- **Exceptions as the transport for system events.** Rejected — an unschedulable check does not abort the scheduler; signalling it with `throw` is control-flow abuse. A *caught* exception is one *source* (the `IJobListener` bridge, §4.7), not the mechanism.
- **A `Level`/`Rank` on `AppRole` for "admin and above."** Rejected for this RFC — a cross-cutting roles-model change with its own blast radius; three named sets (§4.7) cover the need without touching the role schema.

## 8. Risks

- **Coupling to RFC 0008 for tag matching and two system events.** The selector grammar/`Tag` tables (broadcast phase 6) and the `CheckUnschedulable`/`TagReconciliation` emitters are 0008's, unimplemented today. Mitigation: phases 1–5 carry no 0008 dependency (interface split, engine, system framework via the exception bridge, group dispatchers, severity-only broadcast all stand alone); `TagSelectorJson` is nullable so a subscription works on severity alone; the `BackgroundJobFailedEvent` bridge gives the system framework immediate standalone value.
- **Duplicate delivery — paging *and* broadcast for the same alert.** By design an on-call engineer may get a personal page *and* see it in the team channel. Intended, but a channel subscribed with no severity floor buries signal. Mitigation: `MinSeverity` defaults to a non-trivial floor in the UI; a "matches N services" hint warns on very broad subscriptions.
- **The outbox is new infrastructure with its own failure modes.** A stuck `Processing` row (worker crash mid-handle) or a poison event that always throws needs handling. Mitigation: a `Processing` row past a lease timeout is reclaimed to `Pending`; `Attempts` past a cap moves the row to `Failed` (poison-message quarantine) with `LastError` retained, rather than looping forever.
- **Email is the only system-notification channel and can be the broken thing.** If the event *is* "SMTP is misconfigured," its email cannot be delivered. Mitigation: the `SystemNotification` is still *persisted*, so a future in-app feed (§6 phase 7) or a second channel surfaces it. v1 accepts this gap; it is strictly better than today's silence.
- **Un-obsoleting the six group types resurrects config classes that don't exist.** Slack/Discord/etc. have no `[IntegrationManifest]` and no `ConfigType` today. Each needs a real, validated config shape (webhook URL, bot token) in phase 4 — skipping it makes them creatable-but-broken, the exact state RFC 0004 §1 calls out for PagerDuty.
- **Group message rendering is per-provider and unspecified here.** Each group dispatcher must render `AlertNotificationContext` into its provider's format (Slack blocks, Google Chat cards). This RFC defines the contract, not the payloads; poor rendering is a phase-4 quality risk and real per-provider work, not a copy of the personal templates.
- **A never-resolved system notification lingers active.** If a condition clears without its inverse event, the row stays `ResolvedAt == null` and recurrences fold silently. Mitigation: pair every raising event with its inverse (`CheckUnschedulableEvent` ↔ `CheckSchedulableAgainEvent`) and treat an unpaired emitter as a review defect — as the alert path pairs record/resolve.
