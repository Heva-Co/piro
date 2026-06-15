using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Evaluates alert thresholds after a check executes and dispatches notifications
/// via registered <see cref="ITriggerDispatcher"/> implementations.
/// </summary>
public class AlertEvaluationService(
    IAlertConfigRepository alertConfigRepository,
    ICheckDataPointRepository dataPointRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    IEnumerable<ITriggerDispatcher> dispatchers,
    ILogger<AlertEvaluationService> logger)
{
    private readonly Dictionary<TriggerType, ITriggerDispatcher> _dispatchers =
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
        else
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

        // Dispatch to each linked trigger
        foreach (var alertConfigTrigger in config.AlertConfigTriggers)
        {
            var trigger = alertConfigTrigger.Trigger;
            if (trigger.Status == "INACTIVE") continue;
            if (!_dispatchers.TryGetValue(trigger.Type, out var dispatcher))
            {
                logger.LogWarning("No dispatcher registered for trigger type {Type}.", trigger.Type);
                continue;
            }

            try
            {
                await dispatcher.DispatchAsync(trigger, context, ct);
                logger.LogInformation("Trigger {TriggerName} ({TriggerType}) dispatched for check {CheckName} — {Event}.",
                    trigger.Name, trigger.Type, check.Name, shouldRecover ? "recovery" : "alert");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dispatcher {Type} failed for trigger {TriggerId}.", trigger.Type, trigger.Id);
            }
        }

        config.IsAlerting = shouldFire;
        await alertConfigRepository.UpdateAsync(config, ct);
    }

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
