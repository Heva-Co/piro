using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Evaluates alert thresholds after a check executes. Each firing AlertConfig produces/updates
/// an <see cref="Alert"/> row (see <see cref="AlertLifecycleService"/>). Linking an Alert to an
/// Incident, and its on-call escalation, are both handled elsewhere: linking is a manual action
/// (see <c>AlertIncidentLinkService</c>), escalation is driven by <see cref="EscalationCheckerService"/>
/// off the Alert's own <c>EscalationCurrentStep</c>/<c>EscalationStepStartedAt</c> fields.
/// </summary>
public class AlertEvaluationService(
    IAlertConfigRepository alertConfigRepository,
    ICheckDataPointRepository dataPointRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    ILogger<AlertEvaluationService> logger,
    AlertLifecycleService alertLifecycleService)
{
    /// <summary>
    /// Called after every check execution. Evaluates all active alert configs for the given check —
    /// records/updates an Alert on firing, resolves it on recovery.
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
        var recentPoints = (await dataPointRepository.GetByCheckIdAsync(checkId, limit: maxThreshold, ct: ct))
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
        bool conditionMet(CheckDataPoint dp) => IsThresholdConditionMet(config, check, dp);

        // Count consecutive points (most recent first) where condition is met or not
        int consecutiveFailures = CountConsecutive(recentPoints, conditionMet);
        int consecutiveSuccesses = CountConsecutive(recentPoints, dp => !conditionMet(dp));

        bool shouldFire = !config.IsAlerting && consecutiveFailures >= config.FailureThreshold;
        // Condition is still met on a later evaluation while already alerting — not a new
        // transition, but the failure is ongoing and must still be recorded (OccurrenceCount)
        // or it freezes at 1 for the entire duration of the outage.
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
            await alertLifecycleService.RecordOccurrenceAsync(config, check, service, message, ct);
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

    private static readonly HashSet<AlertFor> _metricBasedAlertFors = [AlertFor.CertExpiry, AlertFor.FailedNameServers];

    /// <summary>Returns true when a data point's metric meets the alert threshold condition.</summary>
    private bool IsThresholdConditionMet(AlertConfig config, Check check, CheckDataPoint dp)
    {
        if (_metricBasedAlertFors.Contains(config.AlertFor) && !dp.MetricValue.HasValue)
        {
            logger.LogWarning(
                "AlertConfig {AlertConfigId} on check {CheckId} ({CheckName}) evaluates {AlertFor}, but the latest data point has no MetricValue — this alert can never fire until the check's executor reports one.",
                config.Id, check.Id, check.Name, config.AlertFor);
        }

        return config.AlertFor switch
        {
            AlertFor.Status => IsStatusMet(config, dp),
            AlertFor.Latency => IsLatencyMet(config, dp),
            AlertFor.CertExpiry => IsCertExpiryMet(config, dp),
            AlertFor.FailedNameServers => IsFailedNameServersMet(config, dp),
            _ => false
        };
    }

    private static bool IsStatusMet(AlertConfig config, CheckDataPoint dp)
    {
        return Enum.TryParse<ServiceStatus>(config.AlertValue, out var targetStatus) && dp.Status == targetStatus;
    }

    private static bool IsLatencyMet(AlertConfig config, CheckDataPoint dp)
    {
        return double.TryParse(config.AlertValue, out var maxLatency)
            && dp.LatencyMs.HasValue
            && dp.LatencyMs.Value >= maxLatency;
    }

    private static bool IsCertExpiryMet(AlertConfig config, CheckDataPoint dp)
    {
        return double.TryParse(config.AlertValue, out var maxDaysRemaining)
            && dp.MetricValue.HasValue
            && dp.MetricValue.Value <= maxDaysRemaining;
    }

    private static bool IsFailedNameServersMet(AlertConfig config, CheckDataPoint dp)
    {
        return double.TryParse(config.AlertValue, out var maxFailures)
            && dp.MetricValue.HasValue
            && dp.MetricValue.Value >= maxFailures;
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

    /// <summary>
    /// Builds the frozen Alert message from the most recent data point's error, falling back to a
    /// message synthesized from the metric for threshold-based triggers (the check may still be UP,
    /// so the executor never had a failure to describe in ErrorMessage), then to the config description.
    /// </summary>
    private static string? BuildMessage(AlertConfig config, List<CheckDataPoint> recentPoints)
    {
        var latest = recentPoints.FirstOrDefault();
        return latest?.ErrorMessage ?? BuildMetricMessage(config, latest) ?? config.Description;
    }

    private static string? BuildMetricMessage(AlertConfig config, CheckDataPoint? latest)
    {
        if (latest is null) return null;

        return config.AlertFor switch
        {
            AlertFor.Latency when latest.LatencyMs.HasValue =>
                $"Latency {latest.LatencyMs.Value:F0}ms exceeded threshold {config.AlertValue}ms",
            AlertFor.CertExpiry when latest.MetricValue.HasValue =>
                $"Certificate expires in {latest.MetricValue.Value:F0} day(s), at or below threshold {config.AlertValue}",
            AlertFor.FailedNameServers when latest.MetricValue.HasValue =>
                $"{latest.MetricValue.Value:F0} name server(s) failed, at or above threshold {config.AlertValue}",
            _ => null
        };
    }
}
