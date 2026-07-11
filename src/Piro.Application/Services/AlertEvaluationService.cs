using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Evaluates alert thresholds after a check executes and dispatches notifications
/// via registered <see cref="INotificationDispatcher"/> implementations.
/// </summary>
public class AlertEvaluationService(
    IAlertConfigRepository alertConfigRepository,
    ICheckDataPointRepository dataPointRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    IIncidentRepository incidentRepository,
    ISiteConfigRepository siteConfigRepository,
    IEnumerable<INotificationDispatcher> dispatchers,
    ILogger<AlertEvaluationService> logger,
    IncidentAppService incidentAppService)
{
    private readonly Dictionary<IntegrationType, INotificationDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.Type);

    /// <summary>
    /// Called after every check execution. Evaluates all active alert configs for
    /// the given check and fires or resolves notifications as appropriate.
    /// </summary>
    public async Task EvaluateAsync(int checkId, CancellationToken ct = default)
    {
        var alertConfigs = await alertConfigRepository.GetByCheckIdAsync(checkId, ct);
        var activeConfigs = alertConfigs.Where(a => a.IsActive).ToList();
        if (activeConfigs.Count == 0) return;

        var check = await checkRepository.GetByIdAsync(checkId, ct);
        if (check is null) return;

        var service = await serviceRepository.GetByIdAsync(check.ServiceId, ct);
        if (service is null) return;

        // Fetch enough recent data points to evaluate the highest threshold across all configs
        int maxThreshold = activeConfigs.Max(a => Math.Max(a.FailureThreshold, a.SuccessThreshold));
        var recentPoints = (await dataPointRepository.GetByCheckIdAsync(checkId, ct: ct))
            .Take(maxThreshold)
            .ToList();

        foreach (var config in activeConfigs)
        {
            await EvaluateConfigAsync(config, check, service, recentPoints, ct);
        }
    }

    /// <summary>
    /// Evaluates auto-incident creation/closure based solely on the check's new status.
    /// Called from the ingester on every status change — independent of alert configs.
    /// Creates an incident when the check transitions to DOWN/DEGRADED and
    /// <see cref="Check.AutomaticallyCreateIncident"/> is true; closes it on recovery
    /// when <see cref="Check.AutomaticallyCloseIncident"/> is true.
    /// </summary>
    public async Task EvaluateIncidentPolicyAsync(int checkId, ServiceStatus previousStatus, ServiceStatus newStatus, CancellationToken ct = default)
    {
        if (previousStatus == newStatus) return;

        var check = await checkRepository.GetByIdAsync(checkId, ct);
        if (check is null) return;

        var service = await serviceRepository.GetByIdAsync(check.ServiceId, ct);
        if (service is null) return;

        bool isFailure = newStatus is ServiceStatus.DOWN or ServiceStatus.DEGRADED;

        if (check.AutomaticallyCreateIncident && isFailure)
            await HandleAutoCreateIncidentAsync(check, service, ct);
    }

    private async Task EvaluateConfigAsync(
        AlertConfig config,
        Check check,
        Service service,
        List<CheckDataPoint> recentPoints,
        CancellationToken ct)
    {
        bool conditionMet(CheckDataPoint dp) => IsThresholdConditionMet(config, dp);

        // Count consecutive points (most recent first) where condition is met or not
        int consecutiveFailures = CountConsecutive(recentPoints, conditionMet);
        int consecutiveSuccesses = CountConsecutive(recentPoints, dp => !conditionMet(dp));

        bool shouldFire = !config.IsAlerting && consecutiveFailures >= config.FailureThreshold;
        bool shouldRecover = config.IsAlerting && consecutiveSuccesses >= config.SuccessThreshold;

        if (!shouldFire && !shouldRecover) return;

        if (shouldFire)
            logger.LogWarning("Alert fired for check {CheckId} ({CheckName}): {AlertFor} = {AlertValue} after {Failures} consecutive failure(s).",
                check.Id, check.Name, config.AlertFor, config.AlertValue, consecutiveFailures);
        else if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Alert recovered for check {CheckId} ({CheckName}): {AlertFor} = {AlertValue} after {Successes} consecutive success(es).",
                check.Id, check.Name, config.AlertFor, config.AlertValue, consecutiveSuccesses);

        var context = new AlertNotificationContext(
            ServiceName: service.Name,
            CheckName: check.Name,
            CurrentStatus: check.CurrentStatus,
            AlertDescription: config.Description,
            Severity: config.Severity,
            IsRecovery: shouldRecover,
            FiredAt: DateTime.UtcNow,
            CheckId: check.Id,
            AlertValue: config.AlertValue,
            FailureThreshold: config.FailureThreshold,
            SuccessThreshold: config.SuccessThreshold
        );

        // Dispatch to each linked notification channel
        foreach (var alertConfigChannel in config.AlertConfigNotificationChannels)
        {
            var channel = alertConfigChannel.NotificationChannel;
            if (channel.IsInactive) continue;
            if (!_dispatchers.TryGetValue(channel.Type, out var dispatcher))
            {
                logger.LogWarning("No dispatcher registered for notification channel type {Type}.", channel.Type);
                continue;
            }

            try
            {
                await dispatcher.DispatchAsync(channel, context, ct);
                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Notification channel {ChannelName} ({ChannelType}) dispatched for check {CheckName} — {Event}.",
                        channel.Name, channel.Type, check.Name, shouldRecover ? "recovery" : "alert");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dispatcher {Type} failed for notification channel {ChannelId}.", channel.Type, channel.Id);
            }
        }

        config.IsAlerting = shouldFire;
        await alertConfigRepository.UpdateAsync(config, ct);
    }

    /// <summary>
    /// Entry point for auto-incident creation. Reads system settings to determine
    /// correlation mode and incident impact, then delegates to the appropriate handler.
    /// </summary>
    private async Task HandleAutoCreateIncidentAsync(Check check, Service service, CancellationToken ct)
    {
        var settings = await siteConfigRepository.GetAsync(ct);
        var impact = check.Criticality == CheckCriticality.Critical ? ServiceStatus.DOWN : ServiceStatus.DEGRADED;
        var now = DateTimeOffset.UtcNow;

        switch (settings.IncidentCorrelationMode)
        {
            case IncidentCorrelationMode.PerService:
                await EnsurePerServiceIncidentAsync(check, service, impact, now, ct);
                break;

            case IncidentCorrelationMode.Global:
                await HandleGlobalCorrelationAsync(check, service, impact, settings, now, ct);
                break;

            case IncidentCorrelationMode.Hybrid:
            default:
                await HandleHybridCorrelationAsync(check, service, impact, settings, now, ct);
                break;
        }
    }

    /// <summary>
    /// PerService mode: finds an existing open ALERT incident for the service and attaches to it,
    /// or creates a new per-service incident. Publishing is always a separate, manual action.
    /// </summary>
    private async Task EnsurePerServiceIncidentAsync(
        Check check, Service service, ServiceStatus impact, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await incidentRepository.GetOpenAlertIncidentForServiceAsync(service.Id, ct);
        if (existing is not null)
        {
            // Attach this check's service to an existing incident if not already linked
            if (!existing.IncidentServices.Any(s => s.ServiceId == service.Id))
            {
                existing.IncidentServices.Add(new IncidentService
                {
                    ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
                });
                await incidentRepository.UpdateAsync(existing, ct);
                await RecordImpactIfChangedAsync(existing, ct);
            }
            return;
        }

        var incident = await incidentAppService.CreateAlertIncidentAsync(IncidentTitleFactory.Build(check.Type), isGlobal: false, ct);
        incident.IncidentServices.Add(new IncidentService
        {
            ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
        });
        var created = await incidentRepository.CreateAsync(incident, ct);
        await EmitCreatedAsync(created, now, ct);
        await RecordImpactIfChangedAsync(created, ct);
    }

    /// <summary>
    /// Global mode: counts Critical services alerting within the correlation window.
    /// Does NOT create any incident until the threshold is reached — risk is that isolated failures produce no incident.
    /// Once threshold is met, creates or attaches to a single global incident.
    /// </summary>
    private async Task HandleGlobalCorrelationAsync(
        Check check, Service service, ServiceStatus impact,
        SiteConfig settings, DateTimeOffset now, CancellationToken ct)
    {
        var window = now.AddMinutes(-settings.GlobalIncidentCorrelationWindowMinutes);
        var recentAlerts = await incidentRepository.GetRecentAlertIncidentsAsync(window, ct);
        var criticalServiceCount = recentAlerts.Select(i => i.IncidentServices.Select(s => s.ServiceId)).SelectMany(x => x).Distinct().Count() + 1;

        if (criticalServiceCount < settings.GlobalIncidentThreshold)
        {
            // Threshold not reached — do not create any incident yet
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Global correlation: {Count}/{Threshold} critical services — no incident created yet.", criticalServiceCount, settings.GlobalIncidentThreshold);
            return;
        }

        var globalIncident = await incidentRepository.GetOpenGlobalAlertIncidentAsync(ct);
        if (globalIncident is null)
        {
            globalIncident = await incidentAppService.CreateAlertIncidentAsync("Multiple services affected", isGlobal: true, ct);
            foreach (var i in recentAlerts)
                foreach (var s in i.IncidentServices)
                    globalIncident.IncidentServices.Add(new IncidentService
                    {
                        ServiceId = s.ServiceId, Impact = s.Impact, TriggeringCheckId = s.TriggeringCheckId
                    });
            globalIncident = await incidentRepository.CreateAsync(globalIncident, ct);
            await EmitCreatedAsync(globalIncident, now, ct);
            await RecordImpactIfChangedAsync(globalIncident, ct);
        }

        if (!globalIncident.IncidentServices.Any(s => s.ServiceId == service.Id))
        {
            globalIncident.IncidentServices.Add(new IncidentService
            {
                ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
            });
            await incidentRepository.UpdateAsync(globalIncident, ct);
            await RecordImpactIfChangedAsync(globalIncident, ct);
        }
    }

    /// <summary>
    /// Hybrid mode (default): guarantees an incident is always created immediately (per-service),
    /// and elevates to a global incident when the threshold of simultaneously failing services is reached.
    /// Existing per-service incidents within the correlation window are merged into the global via <see cref="IncidentMerge"/> records.
    /// </summary>
    private async Task HandleHybridCorrelationAsync(
        Check check, Service service, ServiceStatus impact,
        SiteConfig settings, DateTimeOffset now, CancellationToken ct)
    {
        // If a global incident is already open, attach directly to it
        var globalIncident = await incidentRepository.GetOpenGlobalAlertIncidentAsync(ct);
        if (globalIncident is not null)
        {
            if (!globalIncident.IncidentServices.Any(s => s.ServiceId == service.Id))
            {
                globalIncident.IncidentServices.Add(new IncidentService
                {
                    ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
                });
                await incidentRepository.UpdateAsync(globalIncident, ct);
                await RecordImpactIfChangedAsync(globalIncident, ct);
            }
            return;
        }

        var window = now.AddMinutes(-settings.GlobalIncidentCorrelationWindowMinutes);
        var recentPerServiceIncidents = await incidentRepository.GetRecentAlertIncidentsAsync(window, ct);
        var affectedServiceCount = recentPerServiceIncidents
            .SelectMany(i => i.IncidentServices.Select(s => s.ServiceId))
            .Distinct().Count() + 1; // +1 for the current service

        if (affectedServiceCount >= settings.GlobalIncidentThreshold)
        {
            // Elevate: create global incident and merge existing per-service incidents into it
            var newGlobal = await incidentAppService.CreateAlertIncidentAsync("Multiple services affected", isGlobal: true, ct);
            newGlobal = await incidentRepository.CreateAsync(newGlobal, ct);
            await EmitCreatedAsync(newGlobal, now, ct);

            foreach (var perService in recentPerServiceIncidents)
            {
                // Move service links to global incident
                foreach (var link in perService.IncidentServices)
                {
                    newGlobal.IncidentServices.Add(new IncidentService
                    {
                        ServiceId = link.ServiceId, Impact = link.Impact, TriggeringCheckId = link.TriggeringCheckId
                    });
                }

                // Record the merge
                await incidentRepository.AddMergeAsync(new IncidentMerge
                {
                    SourceIncidentId = perService.Id,
                    TargetIncidentId = newGlobal.Id,
                    MergedAt = now,
                    Reason = "Automatic correlation"
                }, ct);

                // Hide per-service incidents from status page and mark them as absorbed —
                // a final state, same as Resolved: no further acks/updates/impact changes apply.
                perService.Visibility = IncidentVisibility.Private;
                perService.Status = IncidentStatus.Merged;
                perService.EndDateTime ??= now.ToUnixTimeSeconds();
                await incidentRepository.UpdateAsync(perService, ct);

                // Record symmetric timeline events on both sides of the merge
                await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
                {
                    IncidentId = perService.Id,
                    Type = TimelineEventType.MergedTo,
                    OccurredAt = now,
                    RelatedIncidentId = newGlobal.Id,
                    Visibility = EventVisibility.Private,
                }, ct);
                await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
                {
                    IncidentId = newGlobal.Id,
                    Type = TimelineEventType.MergedFrom,
                    OccurredAt = now,
                    RelatedIncidentId = perService.Id,
                    Visibility = EventVisibility.Private,
                }, ct);
            }

            // Attach current triggering service
            if (!newGlobal.IncidentServices.Any(s => s.ServiceId == service.Id))
            {
                newGlobal.IncidentServices.Add(new IncidentService
                {
                    ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
                });
            }

            await incidentRepository.UpdateAsync(newGlobal, ct);
            await RecordImpactIfChangedAsync(newGlobal, ct);
            logger.LogWarning("Hybrid correlation: elevated {Count} per-service incidents into global incident #{GlobalId}.",
                recentPerServiceIncidents.Count, newGlobal.Id);
        }
        else
        {
            // Below threshold — create or attach to per-service incident
            await EnsurePerServiceIncidentAsync(check, service, impact, now, ct);
        }
    }

    /// <summary>
    /// Recalculates the worst impact across all <see cref="IncidentService"/> entries and records
    /// an <see cref="IncidentImpactChange"/> if the impact has changed. No-op if unchanged.
    /// </summary>
    private async Task RecordImpactIfChangedAsync(Incident incident, CancellationToken ct)
    {
        var worst = incident.IncidentServices
            .Select(s => s.Impact)
            .DefaultIfEmpty(ServiceStatus.DOWN)
            .Aggregate(ServiceStatus.DOWN, (a, b) => (int)b > (int)a ? b : a);

        if (worst == incident.CurrentImpact) return;

        await incidentRepository.AddImpactChangeAsync(incident, new IncidentImpactChange
        {
            IncidentId = incident.Id,
            Impact = worst,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        }, ct);
    }

    /// <summary>Records the Created timeline event for an incident just persisted via <see cref="IIncidentRepository.CreateAsync"/>.</summary>
    private Task EmitCreatedAsync(Incident incident, DateTimeOffset now, CancellationToken ct) =>
        incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = incident.Id,
            Type = TimelineEventType.Created,
            OccurredAt = now,
            Visibility = EventVisibility.Private,
        }, ct);

    /// <summary>Returns true when a data point's metric meets the alert threshold condition.</summary>
    private static bool IsThresholdConditionMet(AlertConfig config, CheckDataPoint dp)
    {
        return config.AlertFor switch
        {
            AlertFor.Status => Enum.TryParse<ServiceStatus>(config.AlertValue, out var targetStatus)
                               && dp.Status == targetStatus,
            AlertFor.Latency => double.TryParse(config.AlertValue, out var maxLatency)
                                && dp.LatencyMs.HasValue
                                && dp.LatencyMs.Value >= maxLatency,
            _ => false
        };
    }

    /// <summary>Counts how many consecutive data points (from the start of the list) satisfy the predicate.</summary>
    private static int CountConsecutive(List<CheckDataPoint> points, Func<CheckDataPoint, bool> predicate)
    {
        int count = 0;
        foreach (var point in points)
        {
            if (predicate(point)) count++;
            else break;
        }
        return count;
    }
}
