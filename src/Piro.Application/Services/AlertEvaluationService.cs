using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Evaluates alert thresholds after a check executes. Each firing AlertConfig produces/updates
/// an <see cref="Alert"/> row (see <see cref="AlertLifecycleService"/>). Linking an Alert to an
/// Incident, and its on-call escalation, are both handled elsewhere: linking is a manual action
/// (see <c>AlertIncidentLinkService</c>), escalation is driven by <see cref="EscalationCheckerService"/>
/// off the Alert's own <c>EscalationCurrentStep</c>/<c>EscalationStepStartedAt</c> fields.
/// <para>
/// The evaluator is generic: a rule names a <see cref="AlertConfig.Dimension"/> and a
/// <see cref="AlertConfig.Comparison"/>, and the same loop handles every check. A Threshold rule reads
/// the dimension's value from the data point's <see cref="CheckDataPoint.Dimensions"/> and compares it
/// using the direction the check declared (which travels with the datum, not the rule); an Equality rule
/// matches the configured value against the data point's <see cref="CheckDataPoint.Status"/>. There is no
/// per-check-type switch.
/// </para>
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
        bool conditionMet(CheckDataPoint dp) => IsConditionMet(config, check, dp);

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
                logger.LogWarning("Alert fired for check {CheckId} ({CheckName}): {Dimension} {Comparison} {AlertValue} after {Failures} consecutive failure(s).",
                    check.Id, check.Name, config.Dimension, config.Comparison, config.AlertValue, consecutiveFailures);

                config.IsAlerting = true;
                await alertConfigRepository.UpdateAsync(config, ct);
            }

            var message = BuildMessage(config, recentPoints);
            await alertLifecycleService.RecordOccurrenceAsync(
                config, check, service, message, ct, escalationPolicyId: service.EscalationPolicyId);
        }
        else if (shouldRecover)
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Alert recovered for check {CheckId} ({CheckName}): {Dimension} {Comparison} {AlertValue} after {Successes} consecutive success(es).",
                    check.Id, check.Name, config.Dimension, config.Comparison, config.AlertValue, consecutiveSuccesses);

            config.IsAlerting = false;
            await alertConfigRepository.UpdateAsync(config, ct);
            await alertLifecycleService.ResolveActiveAlertAsync(config.Id, ct);
        }
    }

    /// <summary>
    /// Returns true when a data point meets the rule's condition. Generic over every check: an Equality
    /// rule matches the configured <see cref="ServiceStatus"/> against the data point's Status; a
    /// Threshold rule looks the dimension's value up by name and compares it using the direction the
    /// check declared for that dimension.
    /// </summary>
    private bool IsConditionMet(AlertConfig config, Check check, CheckDataPoint dp)
    {
        if (config.Comparison == DimensionComparison.Equality)
            return Enum.TryParse<ServiceStatus>(config.AlertValue, out var targetStatus) && dp.Status == targetStatus;

        // Threshold: the value comes from the dimension bag; the direction comes from the check's spec.
        if (!dp.Dimensions.TryGetValue(config.Dimension, out var value))
        {
            logger.LogWarning(
                "AlertConfig {AlertConfigId} on check {CheckId} ({CheckName}) watches dimension \"{Dimension}\", but the latest data point reported no such measurement — this alert can never fire until the check reports it.",
                config.Id, check.Id, check.Name, config.Dimension);
            return false;
        }

        if (!double.TryParse(config.AlertValue, out var threshold)) return false;

        // The direction was copied from the check's DimensionSpec when the rule was created, so the
        // evaluator compares generically with no per-check knowledge.
        return config.Direction == ThresholdDirection.LowerIsWorse
            ? value <= threshold
            : value >= threshold;
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
    /// message synthesized from the dimension for threshold-based triggers (the check may still be UP,
    /// so it never had a failure to describe in ErrorMessage), then to the config description.
    /// </summary>
    private static string? BuildMessage(AlertConfig config, List<CheckDataPoint> recentPoints)
    {
        var latest = recentPoints.FirstOrDefault();
        return latest?.ErrorMessage ?? BuildMetricMessage(config, latest) ?? config.Description;
    }

    private static string? BuildMetricMessage(AlertConfig config, CheckDataPoint? latest)
    {
        if (latest is null || config.Comparison == DimensionComparison.Equality) return null;
        if (!latest.Dimensions.TryGetValue(config.Dimension, out var value)) return null;

        return config.Dimension switch
        {
            "Latency" => $"Latency {value:F0}ms exceeded threshold {config.AlertValue}ms",
            "CertExpiry" => $"Certificate expires in {value:F0} day(s), at or below threshold {config.AlertValue}",
            "FailedNameServers" => $"{value:F0} name server(s) failed, at or above threshold {config.AlertValue}",
            "LastRunAge" => $"Last run was {value:F0}h ago, at or above threshold {config.AlertValue}h",
            "FailedTasks" => $"{value:F0} task(s) failed, at or above threshold {config.AlertValue}",
            _ => $"{config.Dimension} = {value:F0}, threshold {config.AlertValue}",
        };
    }
}
