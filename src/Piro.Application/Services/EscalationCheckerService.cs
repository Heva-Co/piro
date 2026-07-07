using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Fires escalation steps for open incidents that have an assigned escalation policy.
/// Called every minute by <see cref="Piro.Infrastructure.Jobs.EscalationCheckJob"/>.
/// </summary>
public class EscalationCheckerService(
    IEscalationPolicyRepository policyRepo,
    IIncidentRepository incidentRepo,
    OnCallService onCallService,
    IEnumerable<INotificationChannelDispatcher> dispatchers,
    INotificationChannelRepository channelRepo,
    ILogger<EscalationCheckerService> logger)
{
    private readonly Dictionary<IntegrationType, INotificationChannelDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.Type);

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        var policy = await policyRepo.GetSingleAsync(ct);
        if (policy is null)
        {
            logger.LogDebug("Escalation check skipped — no policy configured.");
            return;
        }

        var incidents = await incidentRepo.GetOpenWithEscalationAsync(ct);
        if (incidents.Count == 0)
        {
            logger.LogDebug("Escalation check — no open incidents with policy assigned.");
            return;
        }

        logger.LogInformation("Escalation check started — policy \"{PolicyName}\", {Count} open incident(s).",
            policy.Name, incidents.Count);

        var now = DateTimeOffset.UtcNow;
        var globalChannels = (await channelRepo.GetGlobalAsync(ct))
            .Where(c => !c.IsInactive)
            .ToList();

        foreach (var incident in incidents)
        {
            try
            {
                await ProcessIncidentAsync(incident, policy, globalChannels, now, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escalation check failed for incident #{IncidentId} \"{Title}\".",
                    incident.Id, incident.Title);
            }
        }
    }

    private async Task ProcessIncidentAsync(
        Incident incident,
        EscalationPolicy policy,
        List<NotificationChannel> globalChannels,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var steps = policy.Steps.OrderBy(s => s.Order).ToList();
        if (steps.Count == 0) return;

        // Initialize on first encounter
        if (incident.EscalationCurrentStep is null)
        {
            incident.EscalationCurrentStep = 0;
            incident.EscalationStepStartedAt = new DateTimeOffset(incident.CreatedAt, TimeSpan.Zero);
            await incidentRepo.UpdateAsync(incident, ct);
            logger.LogInformation(
                "Escalation initialized for incident #{IncidentId} \"{Title}\" — starting at step 1 of {Total}.",
                incident.Id, incident.Title, steps.Count);
            return;
        }

        var currentStepIndex = incident.EscalationCurrentStep.Value;

        // Re-escalate after inactivity
        if (policy.ReEscalateAfterInactivityMinutes > 0
            && incident.LastUserActivityAt.HasValue
            && (now - incident.LastUserActivityAt.Value).TotalMinutes >= policy.ReEscalateAfterInactivityMinutes)
        {
            currentStepIndex = 0;
            incident.EscalationCurrentStep = 0;
            incident.EscalationStepStartedAt = now;
            logger.LogInformation(
                "Escalation re-started for incident #{IncidentId} \"{Title}\" — no activity for {Minutes} min, resetting to step 1.",
                incident.Id, incident.Title, policy.ReEscalateAfterInactivityMinutes);
        }

        // Re-escalate after ACK
        if (policy.ReEscalateAfterAckMinutes > 0
            && incident.AcknowledgedAt.HasValue
            && (now - DateTimeOffset.FromUnixTimeSeconds(incident.AcknowledgedAt.Value)).TotalMinutes >= policy.ReEscalateAfterAckMinutes)
        {
            currentStepIndex = 0;
            incident.EscalationCurrentStep = 0;
            incident.EscalationStepStartedAt = now;
            logger.LogInformation(
                "Escalation re-started for incident #{IncidentId} \"{Title}\" — ACK timeout ({Minutes} min), resetting to step 1.",
                incident.Id, incident.Title, policy.ReEscalateAfterAckMinutes);
        }

        // If ACKed and no re-escalation configured, pause
        if (incident.AcknowledgedAt.HasValue && policy.ReEscalateAfterAckMinutes == 0)
        {
            logger.LogDebug(
                "Escalation paused for incident #{IncidentId} \"{Title}\" — acknowledged, re-escalation disabled.",
                incident.Id, incident.Title);
            return;
        }

        if (currentStepIndex >= steps.Count)
        {
            logger.LogDebug(
                "Escalation exhausted for incident #{IncidentId} \"{Title}\" — all {Total} step(s) already fired.",
                incident.Id, incident.Title, steps.Count);
            return;
        }

        var step = steps[currentStepIndex];
        var stepStart = incident.EscalationStepStartedAt ?? now;
        var elapsed = (now - stepStart).TotalMinutes;

        if (elapsed < step.DelayMinutes)
        {
            logger.LogDebug(
                "Escalation waiting for incident #{IncidentId} — step {StepNum}/{Total}, {Elapsed:F1}/{Delay} min elapsed.",
                incident.Id, currentStepIndex + 1, steps.Count, elapsed, step.DelayMinutes);
            return;
        }

        // Dispatch to current on-call users of the step's schedule
        var onCallUsers = await onCallService.GetCurrentOnCallUsersAsync(step.ScheduleId, ct);
        if (onCallUsers.Count == 0)
        {
            logger.LogWarning(
                "Escalation step {StepNum} for incident #{IncidentId} \"{Title}\": no on-call users found in schedule {ScheduleId}.",
                currentStepIndex + 1, incident.Id, incident.Title, step.ScheduleId);
        }
        else
        {
            var names = string.Join(", ", onCallUsers.Select(u => u.UserName ?? u.Email));
            logger.LogInformation(
                "Escalation step {StepNum}/{Total} for incident #{IncidentId} \"{Title}\" — on-call: {OnCallUsers}.",
                currentStepIndex + 1, steps.Count, incident.Id, incident.Title, names);
        }

        var context = BuildContext(incident);

        foreach (var channel in globalChannels)
        {
            if (!_dispatchers.TryGetValue(channel.Type, out var dispatcher)) continue;
            try
            {
                await dispatcher.DispatchAsync(channel, context, ct);
                logger.LogInformation(
                    "Escalation notification sent via {ChannelType} channel \"{ChannelName}\" for incident #{IncidentId}.",
                    channel.Type, channel.Name, incident.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Escalation dispatch failed via {ChannelType} channel \"{ChannelName}\" for incident #{IncidentId}.",
                    channel.Type, channel.Name, incident.Id);
            }
        }

        // Advance step
        var nextIndex = currentStepIndex + 1;
        if (nextIndex < steps.Count)
        {
            incident.EscalationCurrentStep = nextIndex;
            logger.LogInformation(
                "Escalation advanced to step {NextStep}/{Total} for incident #{IncidentId} \"{Title}\".",
                nextIndex + 1, steps.Count, incident.Id, incident.Title);
        }
        else
        {
            logger.LogInformation(
                "Escalation completed all {Total} step(s) for incident #{IncidentId} \"{Title}\" — no further steps.",
                steps.Count, incident.Id, incident.Title);
        }

        incident.EscalationStepStartedAt = now;
        await incidentRepo.UpdateAsync(incident, ct);
    }

    private static AlertNotificationContext BuildContext(Incident incident) => new(
        ServiceName: "Incident",
        CheckName: incident.Title,
        CurrentStatus: incident.CurrentImpact,
        AlertDescription: $"Escalation: incident \"{incident.Title}\" requires attention.",
        Severity: AlertSeverity.Critical,
        IsRecovery: false,
        FiredAt: incident.CreatedAt
    );
}
