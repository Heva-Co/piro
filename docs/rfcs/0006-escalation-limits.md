# RFC 0006 — Escalation limits: per-step retries with a terminal state

Status: Implemented
Author: Arael Espinosa (https://github.com/cl8dep)
Date: 2026-07-17

## 1. Problem

An unresolved, unacknowledged alert can notify on-call users forever.

Escalation is driven by `EscalationCheckJob`, a Quartz job that runs every minute (`WithCronSchedule("0 * * * * ?")`, `src/Piro.Infrastructure/Extensions/InfrastructureServiceExtensions.cs:156-160`) and delegates all work to `EscalationCheckerService.ProcessAlertAsync` (`src/Piro.Application/Services/EscalationCheckerService.cs:54`). Each tick it walks the policy's steps, and when the last step has fired it either idles or — if `EscalationPolicy.ReEscalateAfterInactivityMinutes > 0` (`src/Piro.Domain/Entities/EscalationPolicy.cs:14`) — **resets `EscalationCurrentStep` to 0 and re-runs the entire ladder** (`EscalationCheckerService.cs:110-131`). There is no ceiling on how many times this repeats.

The only things that stop it today:

- **Resolution.** The driving query returns only unresolved alerts — `.Where(a => a.ResolvedAt == null && a.EscalationPolicyId != null)` (`src/Piro.Infrastructure/Persistence/Repositories/AlertRepository.cs:44`). `ResolvedAt` is set by `AlertLifecycleService` when the underlying check recovers.
- **Acknowledgement.** An ACK pauses escalation (`EscalationCheckerService.cs:77-92`) — but only until `ReEscalateAfterInactivityMinutes` of no user activity elapses, at which point the ACK is cleared and the ladder restarts from step 0 (`EscalationCheckerService.cs:93-102`).

So for an alert whose check keeps failing and that nobody acknowledges, on a policy with re-escalation enabled, there is **no natural termination**. Piro has no terminal escalation state: "exhausted" is a computed log string (`EscalationCheckerService.cs:119`), not a persisted state that suppresses further work.

Beyond the infinite loop, the current step model has a subtler gap. A step fires its on-call users **once** and then advances purely on a timer — after `DelayMinutes` elapse, the next step fires regardless of whether the first person even saw the notification (`EscalationCheckerService.cs:137-144, 231-247`). There is no notion of *insisting* with a person before moving on: you either notify once and move on by the clock, or (via re-escalation) blast the whole ladder again from scratch. What on-call actually wants sits between those two — "page the primary a few times; if they don't pick up, *then* move to the next person."

This is what turns one long outage into hundreds of messages. Consider a check on a 1-minute interval whose target stays down, anchored to a service with a policy of a few short-`DelayMinutes` steps and a low `ReEscalateAfterInactivityMinutes`. The failing check does **not** create one alert per tick — `AlertLifecycleService.RecordOccurrenceAsync` folds repeat failures into the same row and just bumps `OccurrenceCount` (`src/Piro.Application/Services/AlertLifecycleService.cs:38-43`). The volume comes entirely from the escalation loop: it fires each step, exhausts, waits out the inactivity window, resets to step 0, and cycles again — every cycle re-notifying every on-call user across every step, one `DispatchPersonalAsync` per user per step (`EscalationCheckerService.cs:192`). Over a couple of hours that is trivially hundreds of individual notifications, none of which a human asked to keep receiving.

## 2. Non-goals

- **Deduplicating alerts.** Alert dedup already works — `AlertLifecycleService.RecordOccurrenceAsync` collapses repeat failures into one row (`AlertLifecycleService.cs:38-43`). A failing 1-minute check produces exactly one alert, not hundreds. This RFC does not touch that path.
- **Auto-resolving alerts on a timer.** Closing an alert that nobody acknowledged, purely because time passed, changes incident semantics (an unresolved problem is still unresolved). This RFC *stops notifying* about such an alert without claiming it is resolved. Auto-resolve is a separate, larger discussion.
- **Changing recovery-driven resolution.** `ResolvedAt` and the recovery path (`AlertLifecycleService`) are correct and stay as-is.
- **Re-running the whole ladder after it ends.** When the last step exhausts its retries, escalation stops (terminal `Exhausted` state). A bounded "loop the ladder again N times" was considered and rejected (§7) — per-step retries already give operators the "keep trying" behavior they want, one person at a time, without a second global counter.
- **A separate per-recipient rate limit.** Notification frequency is governed by each step's retry interval (§4.2). A standalone throttle on `(recipient, channel)` would duplicate that control; it is not part of this design (§7).

## 3. Design principle

**Each escalation step insists with its people a bounded number of times before handing off to the next step, and the ladder terminates instead of looping.** The retry interval a step uses to insist *is* the notification-frequency control, so there is exactly one knob per concern and one place — `EscalationCheckerService.ProcessAlertAsync`, the sole alert-dispatch choke point — where it is enforced. No parallel pipeline is introduced.

## 4. Design

The step lifecycle changes from *fire-once-then-advance-on-a-timer* to *insist-up-to-N-times-then-advance*:

```
EscalationCheckJob (every minute)
        │
        ▼
EscalationCheckerService.ProcessAlertAsync(alert)
        │
        ├─ alert.EscalationExhaustedAt != null? ──► skip (terminal) ─────────────┐
        │                                                                         │
        ├─ ACK gate (unchanged): paused unless inactivity re-escalation fires     │
        │                                                                         │
        ▼                                                                         │
   current step = steps[EscalationCurrentStep]                                    │
        │                                                                         │
        ├─ first entry to step? wait DelayMinutes since previous step's end       │
        │                                                                         │
        ├─ time for another attempt? (RetryIntervalMinutes since last attempt)    │
        │      ├─ yes → notify step's on-call users; EscalationStepAttempts++     │
        │      └─ no  → wait                                                       │
        │                                                                         │
        ├─ EscalationStepAttempts >= step.MaxRetries?                             │
        │      ├─ more steps left → advance: EscalationCurrentStep++,             │
        │      │                    reset attempts, stamp step-end time           │
        │      └─ last step        → set alert.EscalationExhaustedAt = now ───────┤
        │                                                                         │
        ▼                                                                         │
   dispatcher.DispatchPersonalAsync(...) ── writes EscalationDeliveryLog ◄────────┘
```

### 4.1 `EscalationStep.MaxRetries` and `EscalationStep.RetryIntervalMinutes`

Two new fields on `EscalationStep` (`src/Piro.Domain/Entities/EscalationStep.cs`), sitting alongside the existing `DelayMinutes`:

```csharp
/// <summary>
/// How many times this step notifies its on-call users before escalation advances to the next
/// step (or, on the last step, marks the alert Exhausted). Attempts are spaced by
/// RetryIntervalMinutes. 1 = notify once then advance — today's fire-once behavior.
/// </summary>
public int MaxRetries { get; set; } = 1;

/// <summary>
/// Minutes between two attempts of THIS step. Distinct from DelayMinutes (which is the wait
/// BEFORE the step starts). Ignored when MaxRetries == 1. 0 = each tick may attempt (bounded
/// only by the job's 1-minute cadence).
/// </summary>
public int RetryIntervalMinutes { get; set; }
```

Each step now carries **three** independent time/count fields, and they are deliberately separate:

- **`DelayMinutes`** (existing, `EscalationStep.cs:13`) — the wait *before this step begins*. For step 0, from the alert firing; for later steps, from the previous step ending. Unchanged in meaning.
- **`MaxRetries`** (new) — how many times *this step* insists with its people before handing off.
- **`RetryIntervalMinutes`** (new) — the spacing *between those attempts within the step*.

Worked example — "page the primary on-call five times; if they never respond, move on":

```
Step 1 — Primary:   DelayMinutes=0, MaxRetries=5, RetryIntervalMinutes=1
   t+0   attempt 1 → Primary
   t+1   attempt 2 → Primary
   t+2   attempt 3 → Primary
   t+3   attempt 4 → Primary
   t+4   attempt 5 → Primary   (retries exhausted, no ACK)
                               → advance; wait Step 2's DelayMinutes
Step 2 — Secondary: DelayMinutes=3, MaxRetries=3, RetryIntervalMinutes=1
   t+7   attempt 1 → Secondary   (t+4 end + 3 delay)
   t+8   attempt 2
   t+9   attempt 3   (retries exhausted, last step, no ACK)
                     → EscalationExhaustedAt = now; escalation stops
```

`MaxRetries` lives on the *step*, not the policy, because "how hard do we insist" is a per-rung decision — insist five times with the primary on-call, once with the manager. Defaulting `MaxRetries = 1` makes the new model reduce exactly to today's fire-once-then-advance behavior, so existing steps behave identically until an admin raises it.

### 4.2 `Alert.EscalationStepAttempts` and `Alert.EscalationExhaustedAt`

The current `EscalationCurrentStep` (`src/Piro.Domain/Entities/Alert.cs:55`) tracks *which* step is active but nothing about *how many attempts* that step has made. Add an attempt counter and the terminal state to `Alert` (alongside the existing escalation block at `Alert.cs:52-70`):

```csharp
/// <summary>How many times the CURRENT escalation step has notified its on-call users. Reset to
/// 0 each time escalation advances to a new step. Compared against EscalationStep.MaxRetries to
/// decide when to hand off to the next step.</summary>
public int EscalationStepAttempts { get; set; }

/// <summary>Set when escalation stops because the LAST step exhausted its retries without an ACK
/// or resolution. While non-null the alert is skipped by the escalation job — a persisted
/// terminal escalation state, distinct from ResolvedAt (the problem is still open). Cleared if
/// the alert is later acknowledged, so a human taking over can still drive escalation.</summary>
public DateTimeOffset? EscalationExhaustedAt { get; set; }
```

`EscalationExhaustedAt` is deliberately **not** `ResolvedAt`: the alert stays in the active set for the UI and for ACK, it simply stops generating notifications. This is the difference between "we've stopped paging you" and "the problem is fixed" — conflating them would hide real outages.

The existing `EscalationStepStartedAt` (`Alert.cs:58`) is reused as the timestamp of the current step's *last attempt* (it already updates on every dispatch at `EscalationCheckerService.cs:246`), so the `RetryIntervalMinutes` gate needs no additional field.

### 4.3 Enforcement in `EscalationCheckerService.ProcessAlertAsync`

The changes are contained to the one method — no new service, no new job:

1. **Skip exhausted alerts first.** After the `steps.Count == 0` guard (`EscalationCheckerService.cs:61`):

   ```csharp
   if (alert.EscalationExhaustedAt.HasValue)
   {
       logger.LogDebug("Escalation halted for alert #{AlertId} — last step exhausted its retries.", alert.Id);
       return; // terminal until ACK (which clears it) or resolution (which drops it from the query)
   }
   ```

2. **Replace the fire-once + timer-advance logic** (`EscalationCheckerService.cs:133-247`) with retry-then-advance. The per-step gate becomes two checks — the pre-step `DelayMinutes` wait and the intra-step `RetryIntervalMinutes` spacing:

   ```csharp
   var step = steps[currentStepIndex];
   var lastEvent = alert.EscalationStepStartedAt ?? now;

   // Before the step's first attempt, honor DelayMinutes; between attempts, honor RetryIntervalMinutes.
   var firstAttempt = alert.EscalationStepAttempts == 0;
   var requiredWait = firstAttempt ? step.DelayMinutes : step.RetryIntervalMinutes;
   if ((now - lastEvent).TotalMinutes < requiredWait)
   {
       await alertRepo.UpdateAsync(alert, ct);
       return; // not time for the next attempt yet
   }

   // ... existing dispatch block (on-call lookup + per-preference DispatchPersonalAsync + delivery log) ...

   alert.EscalationStepAttempts++;
   alert.EscalationStepStartedAt = now;

   // Hand off only after this step's retries are exhausted.
   if (alert.EscalationStepAttempts >= step.MaxRetries)
   {
       var nextIndex = currentStepIndex + 1;
       if (nextIndex < steps.Count)
       {
           alert.EscalationCurrentStep = nextIndex;
           alert.EscalationStepAttempts = 0;
           // EscalationStepStartedAt now marks the previous step's end — the next step's
           // DelayMinutes is measured from here.
       }
       else
       {
           alert.EscalationExhaustedAt = now; // last step done insisting; stop.
       }
   }
   await alertRepo.UpdateAsync(alert, ct);
   ```

   The dispatch block itself (on-call resolution `:147`, per-preference loop `:183-227`, delivery logging) is unchanged — only the surrounding gate/advance logic changes.

3. **Clear the terminal state on ACK.** `AlertAppService.AcknowledgeAsync` (`src/Piro.Application/Services/AlertAppService.cs:131-148`) already sets `AcknowledgedAt`/`LastUserActivityAt`; it additionally clears `EscalationExhaustedAt` and resets `EscalationStepAttempts = 0`, so an acknowledged-then-abandoned alert can resume escalation from a clean state rather than staying permanently silent.

The ACK-pause branch (`EscalationCheckerService.cs:77-102`) and `ReEscalateAfterInactivityMinutes` are left intact — see §4.4 for how they now interact.

### 4.4 Interaction with `ReEscalateAfterInactivityMinutes`

`ReEscalateAfterInactivityMinutes` keeps its exact current meaning: it governs **only the ACK case** — how long after an acknowledgement (with no further human activity) escalation should resume from step 1 (`EscalationCheckerService.cs:77-102`). It does not create infinite loops on its own; the loop today comes from the *exhaustion* branch re-escalating (`:110-131`), and that branch is what this RFC replaces with a terminal `Exhausted` state.

Concretely, the old Case 2 (`:110-131`) — "all steps fired, never acknowledged, re-escalate if inactive" — is removed. Reaching the end of the ladder now means the last step exhausted its retries → `EscalationExhaustedAt` is set → the alert goes quiet. `ReEscalateAfterInactivityMinutes` still fires in the ACK branch, and when it does, it resets to step 0 **and clears `EscalationStepAttempts`** so the restarted ladder gets a fresh retry budget per step.

### 4.5 CRUD and DTO surface

Extend the step DTOs (`src/Piro.Application/DTOs/EscalationPolicyDto.cs`):

- `EscalationStepDto` (record at `:13-19`): add `int MaxRetries`, `int RetryIntervalMinutes`.
- `UpsertEscalationStepRequest` (record at `:28-32`): add `[Range(1, int.MaxValue)] int MaxRetries` and `[Range(0, int.MaxValue)] int RetryIntervalMinutes`.

`EscalationPolicyAppService.UpdateAsync` already rebuilds the policy and its steps wholesale on every update (`src/Piro.Application/Services/EscalationPolicyAppService.cs:57-69`), so it maps the two new scalars with no structural change. `EscalationPolicyExtensions.ToDto()` (`src/Piro.Application/Extensions/EscalationPolicyExtensions.cs:9-15`) maps them onto the step DTO. The admin panel's step editor gains two inputs per step; per Piro's convention its API types regenerate from the OpenAPI spec (`pnpm run generate:api-types` in `apps/admin`), so no hand-written types drift.

### 4.6 What does NOT change

- **The Quartz job and its schedule.** `EscalationCheckJob` and the every-minute cron (`InfrastructureServiceExtensions.cs:156-160`) are untouched — enforcement lives entirely inside the service it already calls.
- **The dispatcher layer.** `INotificationDispatcher` and all seven registered dispatchers (`EmailDispatcher`, `TelegramDispatcher`, `TwilioSmsDispatcher`, `MsTeamsDispatcher`, `OpsgenieDispatcher`, `PushoverDispatcher`, `NtfyDispatcher`) are unchanged — retry logic sits *in front of* `DispatchPersonalAsync`, not inside any dispatcher.
- **The dispatch block itself.** On-call resolution, the per-preference priority loop, and `EscalationDeliveryLog` writes (`EscalationCheckerService.cs:147-228`) are reused verbatim — only the gate and advance logic around them change.
- **Alert dedup and recovery.** `AlertLifecycleService.RecordOccurrenceAsync` and the `ResolvedAt` recovery path are untouched.
- **The ACK behavior and `ReEscalateAfterInactivityMinutes`.** The ACK pause and inactivity re-escalation keep working (§4.4); only the *exhaustion* re-escalation branch is replaced by a terminal state.
- **`DelayMinutes` semantics.** Still the wait before a step begins — the new fields add intra-step retry timing without redefining it.
- **The active-alert query.** `AlertRepository.GetActiveWithServiceEscalationAsync` is unchanged — an exhausted alert stays `ResolvedAt == null` and is still returned, then skipped by the new in-service guard, so it remains visible to the UI and to ACK.
- **The verification-code path.** `SendPersonalMessageAsync` (`UserManagementService.cs:369`) is not touched.

## 5. Data / schema scope

New fields:

- `EscalationStep.MaxRetries` — `int`, `NOT NULL`, default `1` (reduces to today's fire-once behavior).
- `EscalationStep.RetryIntervalMinutes` — `int`, `NOT NULL`, default `0`.
- `Alert.EscalationStepAttempts` — `int`, `NOT NULL`, default `0`.
- `Alert.EscalationExhaustedAt` — `timestamptz`, nullable, default `NULL`.

One EF Core migration adds the four columns. Migrations run on API startup (`AGENTS.md`), so no manual step. Backfill is implicit: existing steps get `MaxRetries = 1` / `RetryIntervalMinutes = 0` (identical to current behavior), and existing alerts get `EscalationStepAttempts = 0` / `EscalationExhaustedAt = NULL`.

No changes to: `EscalationPolicy` (no new policy-level field — everything is per-step), `EscalationStep`'s unique `(PolicyId, Order)` index (`EscalationStepConfiguration.cs:13`), `EscalationDeliveryLog`, `Incident`, `IncidentStatus`, `IntegrationType`, `PersonalNotificationChannel`, `UserNotificationPreference`, `AlertConfig`, `Check`, or the active-alert query.

## 6. Phased plan

1. **Per-step retries + terminal state.** Add `EscalationStep.MaxRetries`/`RetryIntervalMinutes` and `Alert.EscalationStepAttempts`/`EscalationExhaustedAt`; rework the gate/advance logic in `EscalationCheckerService` (§4.3); remove the exhaustion re-escalation branch (§4.4); clear the terminal state on ACK in `AlertAppService.AcknowledgeAsync`; DTO/CRUD wiring; migration. This alone ends the infinite-loop failure mode and delivers the "insist then hand off" behavior. It is the whole core of the RFC and ships as one unit.
2. **Admin UX.** Per-step inputs for `MaxRetries` and `RetryIntervalMinutes` with hints clarifying the three timing fields (before-step vs. between-attempts), and surface `EscalationExhaustedAt` on the alert view ("escalation halted — all steps exhausted, acknowledge to resume") so an exhausted alert is visibly distinct from a resolved one. Deferred because it needs design validation and Phase 1 is already correct headless.

## 7. Alternatives considered

- **Cap the number of full-ladder rounds (a policy-level `MaxRounds`).** Rejected in favor of per-step retries. Counting whole passes over the ladder is coarser than operators want: on-call reasons per person ("insist with the primary, then move to the next"), not per full cycle. Per-step `MaxRetries` expresses that directly, and reaching the end of the ladder becoming terminal removes the need for a second global counter.
- **A separate per-recipient rate limit (`NotificationCooldownMinutes`).** Rejected — `RetryIntervalMinutes` already governs how often a step notifies its people, so a standalone frequency throttle would be a redundant second control over the same thing. In this model a person is only notified by the step(s) they're on-call for, at that step's retry cadence; there is no path that bursts them faster.
- **Auto-resolve the alert after the ladder ends.** Rejected — setting `ResolvedAt` would drop the alert from the active query entirely (`AlertRepository.cs:44`) and signal "fixed" when the underlying check is still failing. `EscalationExhaustedAt` stops the paging without lying about the incident's state (§4.2).
- **Reuse `EscalationCurrentStep` as the attempt counter.** Rejected — it tracks *which* step is active, not how many attempts it has made; the two are independent, so a dedicated `EscalationStepAttempts` is required (§4.2).
- **Fold retry interval into `DelayMinutes`.** Rejected — they are genuinely different waits: `DelayMinutes` is the gap *before a step starts* (hand-off delay), `RetryIntervalMinutes` is the gap *between attempts within a step*. Overloading one field would make "wait 3 min before paging the manager, but retry the primary every 1 min" impossible to express.
- **Put `MaxRetries` on the policy instead of the step.** Rejected — insistence is a per-rung property (five tries for the primary, one for the manager). A single policy-wide value can't express that.

## 8. Risks

- **A genuinely un-acked, un-recovered outage goes silent once the ladder exhausts.** By design — but if nobody acknowledged after every step insisted its full budget, the org has an on-call coverage gap that more paging wouldn't fix. Mitigation: Phase 2 surfaces the exhausted state prominently, and ACK resets attempts + clears the terminal state (§4.3) so a human taking over restores escalation. A policy that must never go silent can set a high `MaxRetries` on its final step.
- **Three timing fields per step are easy to misconfigure.** `DelayMinutes`, `MaxRetries`, and `RetryIntervalMinutes` interact, and an operator could confuse "before the step" with "between attempts." The Phase 2 UI must label them unambiguously and ideally preview the resulting schedule (as in §4.1's worked example).
- **Attempt cadence is floored by the job interval.** `RetryIntervalMinutes = 0` does not mean "retry instantly" — the job only runs once a minute (`InfrastructureServiceExtensions.cs:156-160`), so the fastest possible retry is ~1 minute. This is fine (sub-minute paging would be abusive) but must be documented so `0` isn't read as a tight loop.
- **ACK resets the retry budget.** Clearing `EscalationStepAttempts` on ACK (§4.3) means an alert repeatedly acked-and-abandoned could page across several fresh budgets over a very long incident. Intentional — each human hand-off is a fresh decision to keep the alert live — but it means retries bound insistence *per ACK cycle*, not per alert lifetime. Worth stating so it isn't mistaken for a hard lifetime cap.
- **Long steps delay hand-off during a real outage.** A step with `MaxRetries=5, RetryIntervalMinutes=5` holds escalation on one person for ~25 minutes before the next rung is even tried. That is the operator's explicit choice, but the trade-off (thoroughness vs. time-to-next-responder) should be visible in the UI so it's made deliberately.
