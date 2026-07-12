using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Attributes;
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
    IEnumerable<INotificationDispatcher> dispatchers,
    IUserNotificationPreferenceRepository prefRepo,
    ISiteUrlBuilder siteUrlBuilder,
    ILogger<EscalationCheckerService> logger)
{
    private readonly Dictionary<IntegrationType, INotificationDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.Type);

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
        var policy = alert.Service.EscalationPolicy!;
        var steps = policy.Steps.OrderBy(s => s.Order).ToList();
        if (steps.Count == 0) return;

        // Initialize on first encounter — fall through immediately so step 0 fires in the same tick
        if (alert.EscalationCurrentStep is null)
        {
            alert.EscalationCurrentStep = 0;
            alert.EscalationStepStartedAt = alert.FiredAt;
            logger.LogInformation(
                "Escalation initialized for alert #{AlertId} ({Service}/{Check}) — starting at step 1 of {Total}.",
                alert.Id, alert.Service.Name, alert.Check.Name, steps.Count);
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
            alert.AcknowledgedAt = null;
            alert.AcknowledgedBy = null;
            logger.LogInformation(
                "Escalation re-started for alert #{AlertId} — no activity for {Minutes} min since ACK, resetting to step 1.",
                alert.Id, policy.ReEscalateAfterInactivityMinutes);
        }
        // Case 1: mid-policy, never acknowledged — the normal step/delay mechanism governs, inactivity never interrupts it.
        else if (currentStepIndex < steps.Count)
        {
            // fall through to normal step dispatch below
        }
        // Case 2: policy exhausted (all steps fired) and never acknowledged. EscalationStepStartedAt
        // marks when a step last fired — the first tick after exhaustion, that's also "since exhausted".
        else
        {
            var exhaustedSince = alert.EscalationStepStartedAt ?? now;
            var reEscalate = policy.ReEscalateAfterInactivityMinutes > 0
                && (now - exhaustedSince).TotalMinutes >= policy.ReEscalateAfterInactivityMinutes;

            if (!reEscalate)
            {
                logger.LogDebug(
                    "Escalation exhausted for alert #{AlertId} — all {Total} step(s) already fired.",
                    alert.Id, steps.Count);
                await alertRepo.UpdateAsync(alert, ct);
                return;
            }

            currentStepIndex = 0;
            alert.EscalationCurrentStep = 0;
            alert.EscalationStepStartedAt = now;
            logger.LogInformation(
                "Escalation re-started for alert #{AlertId} — exhausted with no activity for {Minutes} min, resetting to step 1.",
                alert.Id, policy.ReEscalateAfterInactivityMinutes);
        }

        var step = steps[currentStepIndex];
        var stepStart = alert.EscalationStepStartedAt ?? now;
        var elapsed = (now - stepStart).TotalMinutes;

        if (elapsed < step.DelayMinutes)
        {
            logger.LogDebug(
                "Escalation waiting for alert #{AlertId} — step {StepNum}/{Total}, {Elapsed:F1}/{Delay} min elapsed.",
                alert.Id, currentStepIndex + 1, steps.Count, elapsed, step.DelayMinutes);
            await alertRepo.UpdateAsync(alert, ct);
            return;
        }

        // Dispatch to current on-call users of the step's schedule
        var onCallUsers = await onCallService.GetCurrentOnCallUsersAsync(step.ScheduleId, ct);
        if (onCallUsers.Count == 0)
        {
            logger.LogWarning(
                "Escalation step {StepNum} for alert #{AlertId} ({Service}/{Check}): no on-call users found in schedule {ScheduleId}.",
                currentStepIndex + 1, alert.Id, alert.Service.Name, alert.Check.Name, step.ScheduleId);
        }
        else
        {
            var names = string.Join(", ", onCallUsers.Select(u => u.UserName ?? u.Email));
            logger.LogInformation(
                "Escalation step {StepNum}/{Total} for alert #{AlertId} ({Service}/{Check}) — on-call: {OnCallUsers}.",
                currentStepIndex + 1, steps.Count, alert.Id, alert.Service.Name, alert.Check.Name, names);
        }

        var serviceUrl = await siteUrlBuilder.GetUrlAsync(AdminArtifactType.Service, ct, alert.Service.Slug);
        var checkUrl = await siteUrlBuilder.GetUrlAsync(AdminArtifactType.Check, ct, alert.Service.Slug, alert.Check.Slug);

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
            var context = BuildContext(alert, serviceUrl, checkUrl, user.TimeZone);

            foreach (var pref in prefs.OrderBy(p => p.Priority))
            {
                if (!pref.VerifiedAt.HasValue) continue; // unverified handle — never dispatch to it
                if (pref.Channel.RequiresIntegration() && pref.Integration is null) continue;
                var channelType = pref.Channel.ToIntegrationType();
                if (!_dispatchers.TryGetValue(channelType, out var dispatcher)) continue;

                try
                {
                    var sent = await dispatcher.DispatchPersonalAsync(pref.Integration, pref.Handle, context, ct);
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

        // Advance step
        var nextIndex = currentStepIndex + 1;
        if (nextIndex < steps.Count)
        {
            alert.EscalationCurrentStep = nextIndex;
            logger.LogInformation(
                "Escalation advanced to step {NextStep}/{Total} for alert #{AlertId} ({Service}/{Check}).",
                nextIndex + 1, steps.Count, alert.Id, alert.Service.Name, alert.Check.Name);
        }
        else
        {
            logger.LogInformation(
                "Escalation completed all {Total} step(s) for alert #{AlertId} ({Service}/{Check}) — no further steps.",
                steps.Count, alert.Id, alert.Service.Name, alert.Check.Name);
        }

        alert.EscalationStepStartedAt = now;
        await alertRepo.UpdateAsync(alert, ct);
    }

    private static AlertNotificationContext BuildContext(
        Alert alert, string? serviceUrl, string? checkUrl, string recipientTimeZone) => new(
        ServiceName: alert.Service.Name,
        CheckName: alert.Check.Name,
        CurrentStatus: alert.ImpactAtFireTime,
        AlertDescription: alert.AlertConfig?.Description ?? alert.Message,
        Severity: alert.AlertConfig?.Severity ?? AlertSeverity.Critical,
        IsRecovery: false,
        FiredAt: alert.FiredAt,
        CheckId: alert.CheckId,
        ServiceUrl: serviceUrl,
        CheckUrl: checkUrl,
        FiredAtDisplay: FormatForTimeZone(alert.FiredAt, recipientTimeZone)
    );

    /// <summary>Formats a timestamp in the given IANA time zone, with the zone name in parentheses. Falls back to UTC if the id is unrecognized.</summary>
    private static string FormatForTimeZone(DateTimeOffset instant, string timeZoneId)
    {
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        return $"{local:yyyy-MM-dd HH:mm} ({timeZoneId})";
    }
}
