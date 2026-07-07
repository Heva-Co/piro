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
        if (policy is null) return;

        var incidents = await incidentRepo.GetOpenWithEscalationAsync(ct);
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
                logger.LogError(ex, "Escalation check failed for incident {IncidentId}.", incident.Id);
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
            logger.LogInformation("Incident {Id}: re-escalating due to inactivity.", incident.Id);
        }

        // Re-escalate after ACK
        if (policy.ReEscalateAfterAckMinutes > 0
            && incident.AcknowledgedAt.HasValue
            && (now - DateTimeOffset.FromUnixTimeSeconds(incident.AcknowledgedAt.Value)).TotalMinutes >= policy.ReEscalateAfterAckMinutes)
        {
            currentStepIndex = 0;
            incident.EscalationCurrentStep = 0;
            incident.EscalationStepStartedAt = now;
            logger.LogInformation("Incident {Id}: re-escalating after ACK timeout.", incident.Id);
        }

        // If ACKed and no re-escalation configured, pause
        if (incident.AcknowledgedAt.HasValue && policy.ReEscalateAfterAckMinutes == 0) return;

        if (currentStepIndex >= steps.Count) return;

        var step = steps[currentStepIndex];
        var stepStart = incident.EscalationStepStartedAt ?? now;
        var elapsed = (now - stepStart).TotalMinutes;

        if (elapsed < step.DelayMinutes) return;

        // Dispatch to current on-call users of the step's schedule
        var onCallUsers = await onCallService.GetCurrentOnCallUsersAsync(step.ScheduleId, ct);
        if (onCallUsers.Count == 0)
        {
            logger.LogWarning("Incident {Id}: no on-call users for schedule {ScheduleId} at step {Step}.",
                incident.Id, step.ScheduleId, currentStepIndex);
        }

        var context = BuildContext(incident);

        foreach (var channel in globalChannels)
        {
            if (!_dispatchers.TryGetValue(channel.Type, out var dispatcher)) continue;
            try
            {
                await dispatcher.DispatchAsync(channel, context, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escalation dispatch failed for channel {ChannelId}.", channel.Id);
            }
        }

        logger.LogInformation(
            "Incident {Id}: dispatched escalation step {Step} (schedule {ScheduleId}), {OnCallCount} on-call user(s).",
            incident.Id, currentStepIndex, step.ScheduleId, onCallUsers.Count);

        // Advance step
        var nextIndex = currentStepIndex + 1;
        incident.EscalationCurrentStep = nextIndex < steps.Count ? nextIndex : currentStepIndex;
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
