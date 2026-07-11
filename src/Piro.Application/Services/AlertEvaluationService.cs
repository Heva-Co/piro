using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Evaluates alert thresholds after a check executes. Each firing AlertConfig produces/updates
/// an <see cref="Alert"/> row (see <see cref="AlertLifecycleService"/>), independent of whether
/// it is ever hooked to an Incident. An Alert is hooked to an Incident once it crosses either
/// its own <see cref="AlertConfig.IncidentThresholdOccurrences"/>, or once enough services are
/// concurrently alerting (see <see cref="IncidentCorrelationMode"/>).
/// </summary>
public class AlertEvaluationService(
    IAlertConfigRepository alertConfigRepository,
    ICheckDataPointRepository dataPointRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    IIncidentRepository incidentRepository,
    IAlertRepository alertRepository,
    ISiteConfigRepository siteConfigRepository,
    ILogger<AlertEvaluationService> logger,
    IncidentAppService incidentAppService,
    AlertLifecycleService alertLifecycleService)
{
    /// <summary>
    /// Called after every check execution. Evaluates all active alert configs for the given check —
    /// records/updates an Alert on firing, resolves it on recovery, and evaluates whether the
    /// resulting Alert should be hooked to an Incident.
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
        // Condition is still met on a later evaluation while already alerting — not a new
        // transition, but the failure is ongoing and must still be recorded (OccurrenceCount,
        // incident-threshold check) or it freezes at 1 for the entire duration of the outage.
        bool stillFailing = config.IsAlerting && recentPoints.Count > 0 && conditionMet(recentPoints[0]);
        bool shouldRecover = config.IsAlerting && consecutiveSuccesses >= config.SuccessThreshold;

        if (!shouldFire && !stillFailing && !shouldRecover) return;

        if (shouldFire || stillFailing)
        {
            if (shouldFire)
            {
                logger.LogWarning("Alert fired for check {CheckId} ({CheckName}): {AlertFor} = {AlertValue} after {Failures} consecutive failure(s).",
                    check.Id, check.Name, config.AlertFor, config.AlertValue, consecutiveFailures);

                config.IsAlerting = true;
                await alertConfigRepository.UpdateAsync(config, ct);
            }

            var message = BuildMessage(config, recentPoints);
            var alert = await alertLifecycleService.RecordOccurrenceAsync(config, check, service, message, ct);

            if (config.CreateIncident && alert.IncidentId is null)
                await EvaluateIncidentHookAsync(alert, config, check, service, ct);
        }
        else if (shouldRecover)
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Alert recovered for check {CheckId} ({CheckName}): {AlertFor} = {AlertValue} after {Successes} consecutive success(es).",
                    check.Id, check.Name, config.AlertFor, config.AlertValue, consecutiveSuccesses);

            config.IsAlerting = false;
            await alertConfigRepository.UpdateAsync(config, ct);
            await alertLifecycleService.ResolveActiveAlertAsync(config.Id, ct);
        }
    }

    /// <summary>
    /// Decides whether the given Alert should be hooked to an Incident — either because it
    /// individually crossed its own occurrence threshold, or because enough services are
    /// concurrently alerting to warrant a merge — then delegates to the correlation mode
    /// currently configured.
    /// </summary>
    private async Task EvaluateIncidentHookAsync(Alert alert, AlertConfig config, Check check, Service service, CancellationToken ct)
    {
        var settings = await siteConfigRepository.GetAsync(ct);

        bool byOccurrence = alert.OccurrenceCount >= config.IncidentThresholdOccurrences;
        bool byConcurrentCount = await alertRepository.CountConcurrentActiveAlertingServicesAsync(ct) >= settings.MergeThreshold;

        if (!byOccurrence && !byConcurrentCount) return;

        var now = DateTimeOffset.UtcNow;
        var incident = settings.IncidentCorrelationMode switch
        {
            IncidentCorrelationMode.PerService => await EnsurePerServiceIncidentAsync(alert, check, service, alert.ImpactAtFireTime, now, ct),
            IncidentCorrelationMode.Merge or _ => await HandleMergeCorrelationAsync(alert, check, service, alert.ImpactAtFireTime, settings, now, ct),
        };

        alert.IncidentId = incident.Id;
        await alertRepository.UpdateAsync(alert, ct);
        await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
        {
            IncidentId = incident.Id,
            Type = TimelineEventType.AlertFired,
            OccurredAt = now,
            AlertId = alert.Id,
            Visibility = EventVisibility.Private,
        }, ct);
    }

    /// <summary>
    /// PerService mode: finds an existing open ALERT incident for the service and attaches to it,
    /// or creates a new per-service incident. Publishing is always a separate, manual action.
    /// </summary>
    private async Task<Incident> EnsurePerServiceIncidentAsync(
        Alert alert, Check check, Service service, ServiceStatus impact, DateTimeOffset now, CancellationToken ct)
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
            return existing;
        }

        var incident = await incidentAppService.CreateAlertIncidentAsync(IncidentTitleFactory.Build(check.Type), ct);
        incident.IncidentServices.Add(new IncidentService
        {
            ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
        });
        var created = await incidentRepository.CreateAsync(incident, ct);
        await EmitCreatedAsync(created, now, ct);
        await RecordImpactIfChangedAsync(created, ct);
        return created;
    }

    /// <summary>
    /// Merge mode (default): guarantees an incident is always created immediately (per-service),
    /// and merges recent per-service incidents into a single incident — reflecting exactly their
    /// combined services, never "all services" — once the threshold of simultaneously failing
    /// services is reached within the correlation window. Existing per-service incidents are
    /// merged in via <see cref="IncidentMerge"/> records.
    /// </summary>
    private async Task<Incident> HandleMergeCorrelationAsync(
        Alert alert, Check check, Service service, ServiceStatus impact,
        SiteConfig settings, DateTimeOffset now, CancellationToken ct)
    {
        var window = now.AddMinutes(-settings.MergeCorrelationWindowMinutes);
        var recentPerServiceIncidents = await incidentRepository.GetRecentAlertIncidentsAsync(window, ct);
        var affectedServiceCount = recentPerServiceIncidents
            .SelectMany(i => i.IncidentServices.Select(s => s.ServiceId))
            .Distinct().Count() + 1; // +1 for the current service

        if (affectedServiceCount < settings.MergeThreshold)
            return await EnsurePerServiceIncidentAsync(alert, check, service, impact, now, ct);

        // Merge: create a new incident that aggregates EXACTLY the correlated services — never "all".
        var merged = await incidentAppService.CreateAlertIncidentAsync("Multiple correlated services affected", ct);
        merged = await incidentRepository.CreateAsync(merged, ct);
        await EmitCreatedAsync(merged, now, ct);

        foreach (var perService in recentPerServiceIncidents)
        {
            foreach (var link in perService.IncidentServices)
            {
                merged.IncidentServices.Add(new IncidentService
                {
                    ServiceId = link.ServiceId, Impact = link.Impact, TriggeringCheckId = link.TriggeringCheckId
                });
            }

            await incidentRepository.AddMergeAsync(new IncidentMerge
            {
                SourceIncidentId = perService.Id,
                TargetIncidentId = merged.Id,
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
                RelatedIncidentId = merged.Id,
                Visibility = EventVisibility.Private,
            }, ct);
            await incidentRepository.AddTimelineEventAsync(new IncidentTimelineEvent
            {
                IncidentId = merged.Id,
                Type = TimelineEventType.MergedFrom,
                OccurredAt = now,
                RelatedIncidentId = perService.Id,
                Visibility = EventVisibility.Private,
            }, ct);
        }

        // Attach current triggering service
        if (!merged.IncidentServices.Any(s => s.ServiceId == service.Id))
        {
            merged.IncidentServices.Add(new IncidentService
            {
                ServiceId = service.Id, Impact = impact, TriggeringCheckId = check.Id
            });
        }

        await incidentRepository.UpdateAsync(merged, ct);
        await RecordImpactIfChangedAsync(merged, ct);
        logger.LogWarning("Merge correlation: merged {Count} per-service incidents into incident #{MergedId}.",
            recentPerServiceIncidents.Count, merged.Id);
        return merged;
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

    /// <summary>Builds the frozen Alert message from the most recent data point's error, falling back to the config description.</summary>
    private static string? BuildMessage(AlertConfig config, List<CheckDataPoint> recentPoints) =>
        recentPoints.FirstOrDefault()?.ErrorMessage ?? config.Description;
}
