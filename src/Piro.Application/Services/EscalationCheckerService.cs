using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Fires escalation steps for active Alerts whose Service has an escalation policy assigned.
/// Called every minute by <see cref="Piro.Infrastructure.Jobs.EscalationCheckJob"/>.
/// The Alert — not any Incident it may later be attached to — owns its own escalation progress,
/// since notifying on-call users must not depend on whether a human has manually created/linked
/// an Incident yet.
/// </summary>
public class EscalationCheckerService(
    IAlertRepository alertRepo,
    OnCallService onCallService,
    IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>> dispatchers,
    IUserNotificationPreferenceRepository prefRepo,
    ISiteUrlBuilder siteUrlBuilder,
    ILogger<EscalationCheckerService> logger)
{
    private readonly Dictionary<string, IPersonalNotificationDispatcher<AlertNotificationContext>> _dispatchers =
        dispatchers.ToDictionary(d => d.IntegrationId);

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        var alerts = await alertRepo.GetActiveWithServiceEscalationAsync(ct);
        if (alerts.Count == 0)
        {
            logger.LogDebug("Escalation check — no active alerts with a service escalation policy.");
            return;
        }

        logger.LogDebug("Escalation check started — {Count} active alert(s) with a policy.", alerts.Count);

        var now = DateTimeOffset.UtcNow;
        foreach (var alert in alerts)
        {
            try
            {
                await ProcessAlertAsync(alert, now, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escalation check failed for alert #{AlertId} (check {CheckId}).",
                    alert.Id, alert.CheckId);
            }
        }
    }

    private async Task ProcessAlertAsync(Alert alert, DateTimeOffset now, CancellationToken ct)
    {
        // Snapshotted at creation (from Service.EscalationPolicyId or Integration.EscalationPolicyId
        // for orphan alerts) — never re-resolved via Service/Integration, so this alert's escalation
        // stays deterministic even if the source policy changes mid-flight. See RFC 0001 §4.6.
        var policy = alert.EscalationPolicy!;
        var steps = policy.Steps.OrderBy(s => s.Order).ToList();
        if (steps.Count == 0) return;

        // Terminal state: the last step exhausted its retries with no ACK/resolution. Stays in the
        // active query (still ResolvedAt == null) and visible to the UI, but generates no more
        // notifications until ACK clears it (AlertAppService.AcknowledgeAsync) or the check recovers.
        if (alert.EscalationExhaustedAt.HasValue)
        {
            logger.LogDebug("Escalation halted for alert #{AlertId} — last step exhausted its retries.", alert.Id);
            return;
        }

        // Initialize on first encounter — fall through immediately so step 0 fires in the same tick
        if (alert.EscalationCurrentStep is null)
        {
            alert.EscalationCurrentStep = 0;
            alert.EscalationStepStartedAt = alert.FiredAt;
            logger.LogInformation(
                "Escalation initialized for alert #{AlertId} ({Service}/{Check}) — starting at step 1 of {Total}.",
                alert.Id, alert.ServiceLabel(), alert.CheckLabel(), steps.Count);
        }

        var currentStepIndex = alert.EscalationCurrentStep.Value;

        // Case 3: paused by an ACK. Never lets the current step advance — the only way past it is
        // ReEscalateAfterInactivityMinutes elapsing since the ACK (LastUserActivityAt).
        if (alert.AcknowledgedAt.HasValue)
        {
            var inactiveSinceAck = policy.ReEscalateAfterInactivityMinutes > 0
                && alert.LastUserActivityAt.HasValue
                && (now - alert.LastUserActivityAt.Value).TotalMinutes >= policy.ReEscalateAfterInactivityMinutes;

            if (!inactiveSinceAck)
            {
                logger.LogDebug(
                    "Escalation paused for alert #{AlertId} — acknowledged{ReescalateNote}.",
                    alert.Id, policy.ReEscalateAfterInactivityMinutes > 0
                        ? $", re-escalates after {policy.ReEscalateAfterInactivityMinutes} min of inactivity"
                        : ", re-escalation disabled");
                await alertRepo.UpdateAsync(alert, ct);
                return;
            }

            currentStepIndex = 0;
            alert.EscalationCurrentStep = 0;
            alert.EscalationStepStartedAt = now;
            alert.EscalationStepAttempts = 0; // fresh retry budget per step on the restarted ladder
            alert.AcknowledgedAt = null;
            alert.AcknowledgedBy = null;
            logger.LogInformation(
                "Escalation re-started for alert #{AlertId} — no activity for {Minutes} min since ACK, resetting to step 1.",
                alert.Id, policy.ReEscalateAfterInactivityMinutes);
        }
        // Otherwise: mid-policy, never acknowledged — the normal step/retry mechanism governs below.
        // (There is no longer an "exhausted, re-escalate the whole ladder" branch: reaching the end
        // of the ladder now sets EscalationExhaustedAt and the alert goes quiet — see RFC 0006 §4.4.)

        var step = steps[currentStepIndex];
        var lastEvent = alert.EscalationStepStartedAt ?? now;

        // Before the step's first attempt, honor DelayMinutes (the pre-step hand-off wait); between
        // attempts of the same step, honor RetryIntervalMinutes (the intra-step spacing).
        var firstAttempt = alert.EscalationStepAttempts == 0;
        var requiredWait = firstAttempt ? step.DelayMinutes : step.RetryIntervalMinutes;
        var elapsed = (now - lastEvent).TotalMinutes;
        if (elapsed < requiredWait)
        {
            logger.LogDebug(
                "Escalation waiting for alert #{AlertId} — step {StepNum}/{Total}, attempt {Attempt}/{MaxRetries}, {Elapsed:F1}/{Wait} min elapsed.",
                alert.Id, currentStepIndex + 1, steps.Count, alert.EscalationStepAttempts + 1, step.MaxRetries, elapsed, requiredWait);
            await alertRepo.UpdateAsync(alert, ct);
            return;
        }

        // Dispatch to current on-call users of the step's schedule
        var onCallUsers = await onCallService.GetCurrentOnCallUsersAsync(step.ScheduleId, ct);
        if (onCallUsers.Count == 0)
        {
            logger.LogWarning(
                "Escalation step {StepNum} for alert #{AlertId} ({Service}/{Check}): no on-call users found in schedule {ScheduleId}.",
                currentStepIndex + 1, alert.Id, alert.ServiceLabel(), alert.CheckLabel(), step.ScheduleId);
        }
        else
        {
            var names = string.Join(", ", onCallUsers.Select(u => u.UserName ?? u.Email));
            logger.LogInformation(
                "Escalation step {StepNum}/{Total} for alert #{AlertId} ({Service}/{Check}) — on-call: {OnCallUsers}.",
                currentStepIndex + 1, steps.Count, alert.Id, alert.ServiceLabel(), alert.CheckLabel(), names);
        }

        var serviceUrl = alert.Service is not null
            ? await siteUrlBuilder.GetUrlAsync(AdminArtifactType.Service, ct, alert.Service.Slug)
            : null;
        var checkUrl = alert.Service is not null && alert.Check is not null
            ? await siteUrlBuilder.GetUrlAsync(AdminArtifactType.Check, ct, alert.Service.Slug, alert.Check.Slug)
            : null;
        var alertUrl = await siteUrlBuilder.GetUrlAsync(AdminArtifactType.Alert, ct, alert.Id.ToString());

        // Notify each on-call user via their personal preferences (ordered by priority). No
        // fallback to any global channel — if a user has no personal preference configured,
        // they simply don't get notified.
        // Batched instead of one GetByUserIdAsync call per user — this runs every minute per
        // active alert, so an N+1 here scales with (active alerts) x (on-call users per schedule).
        var prefsByUser = await prefRepo.GetByUserIdsAsync(onCallUsers.Select(u => u.Id).ToList(), ct);
        foreach (var user in onCallUsers)
        {
            if (!prefsByUser.TryGetValue(user.Id, out var prefs) || prefs.Count == 0) continue;

            // Built per-user: FiredAtDisplay depends on each on-call user's own time zone.
            var context = alert.ToNotificationContext(serviceUrl, checkUrl, alertUrl, FormatForTimeZone(alert.FiredAt, user.TimeZone));

            foreach (var pref in prefs.OrderBy(p => p.Priority))
            {
                if (!pref.VerifiedAt.HasValue) continue; // unverified handle — never dispatch to it
                if (pref.Channel.RequiresIntegration() && pref.Integration is null) continue;
                var channelType = pref.Channel.ToIntegrationType();
                if (!_dispatchers.TryGetValue(channelType.ToString(), out var dispatcher)) continue;

                try
                {
                    var sent = await dispatcher.SendAsync(pref.Integration, pref.Handle, context, ct);
                    if (!sent) continue; // dispatcher doesn't support personal dispatch; try next

                    logger.LogInformation(
                        "Escalation personal notification sent to {UserName} via {ChannelType} for alert #{AlertId}.",
                        user.UserName ?? user.Email, channelType, alert.Id);
                    await alertRepo.AddDeliveryLogAsync(new EscalationDeliveryLog
                    {
                        AlertId = alert.Id,
                        StepIndex = currentStepIndex,
                        UserId = user.Id,
                        UserName = user.UserName ?? user.Email,
                        ChannelType = channelType,
                        Succeeded = true,
                        AttemptedAt = now,
                    }, ct);
                    break; // Only use first working preference per user
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Escalation personal dispatch failed for {UserName} via {ChannelType} for alert #{AlertId} — trying next preference.",
                        user.UserName ?? user.Email, channelType, alert.Id);
                    await alertRepo.AddDeliveryLogAsync(new EscalationDeliveryLog
                    {
                        AlertId = alert.Id,
                        StepIndex = currentStepIndex,
                        UserId = user.Id,
                        UserName = user.UserName ?? user.Email,
                        ChannelType = channelType,
                        Succeeded = false,
                        ErrorMessage = ex.Message,
                        AttemptedAt = now,
                    }, ct);
                }
            }
        }

        // Record this attempt. EscalationStepStartedAt doubles as the last-attempt timestamp that
        // gates RetryIntervalMinutes (and, once we advance, marks the previous step's end for the
        // next step's DelayMinutes).
        alert.EscalationStepAttempts++;
        alert.EscalationStepStartedAt = now;

        // Hand off only after this step has insisted its full retry budget.
        if (alert.EscalationStepAttempts >= step.MaxRetries)
        {
            var nextIndex = currentStepIndex + 1;
            if (nextIndex < steps.Count)
            {
                alert.EscalationCurrentStep = nextIndex;
                alert.EscalationStepAttempts = 0;
                logger.LogInformation(
                    "Escalation advanced to step {NextStep}/{Total} for alert #{AlertId} ({Service}/{Check}).",
                    nextIndex + 1, steps.Count, alert.Id, alert.ServiceLabel(), alert.CheckLabel());
            }
            else
            {
                alert.EscalationExhaustedAt = now; // last step done insisting — escalation stops (terminal).
                logger.LogInformation(
                    "Escalation exhausted for alert #{AlertId} ({Service}/{Check}) — last step insisted {MaxRetries} time(s), no ACK. Halting.",
                    alert.Id, alert.ServiceLabel(), alert.CheckLabel(), step.MaxRetries);
            }
        }

        await alertRepo.UpdateAsync(alert, ct);
    }

    /// <summary>
    /// Formats a timestamp in the given IANA time zone, with the zone name in parentheses. Falls back to UTC if the id is unrecognized.
    /// </summary>
    private static string FormatForTimeZone(DateTimeOffset instant, string timeZoneId)
    {
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        return $"{local:yyyy-MM-dd HH:mm} ({timeZoneId})";
    }
}
