using System.Threading.Channels;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Persists a check execution result: creates a minute-aligned data point,
/// updates <see cref="Check.CurrentStatus"/>, fires a
/// <see cref="CheckStatusChangedEvent"/>, and evaluates alert thresholds.
/// </summary>
public class CheckResultIngesterService(
    ICheckRepository checkRepo,
    ICheckDataPointRepository dataPointRepo,
    Channel<CheckStatusChangedEvent> statusChannel,
    AlertEvaluationService alertEvaluationService) : ICheckResultIngester
{
    // ── Full single-region ingestion ──────────────────────────────────────────

    public async Task IngestAsync(int checkId, CheckExecutionResult result, string workerRegion, CancellationToken ct = default)
    {
        await IngestDataPointOnlyAsync(checkId, result, workerRegion, cycleTimestamp: null, ct);
        await IngestStatusOnlyAsync(checkId, result, ct);
    }

    // ── Per-region data point persistence (multi-region step 1) ──────────────

    public async Task IngestDataPointOnlyAsync(int checkId, CheckExecutionResult result, string workerRegion, long? cycleTimestamp = null, CancellationToken ct = default)
    {
        // Use the cycle timestamp sealed at dispatch when present, so every region of one multi-region
        // cycle lands on the same minute bucket regardless of per-region ingestion delay; otherwise floor
        // the current time (single-region path).
        long timestamp;
        if (cycleTimestamp is { } sealedTs)
        {
            timestamp = sealedTs;
        }
        else
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            timestamp -= timestamp % 60;
        }

        var dataPoint = new CheckDataPoint
        {
            CheckId = checkId,
            Timestamp = timestamp,
            Status = result.Status,
            Dimensions = new Dictionary<string, double>(result.Dimensions),
            DataType = DataPointType.REALTIME,
            ErrorMessage = result.ErrorMessage,
            WorkerRegion = workerRegion
        };

        await dataPointRepo.CreateAsync(dataPoint, ct);
    }

    // ── Status update + events + alerts (multi-region step 2, once per batch) ─

    public async Task IngestStatusOnlyAsync(int checkId, CheckExecutionResult aggregatedResult, CancellationToken ct = default)
    {
        var check = await checkRepo.GetByIdAsync(checkId, ct);
        if (check is null) return;

        // FAILURE means the check itself could not run (bad config, executor/DI error) — it is NOT a
        // measurement of the target, so it must not move CurrentStatus (which would show the check as
        // "Down" for an internal error) nor drive alerts. The FAILURE data point is still recorded (above)
        // and visible in the logs; CurrentStatus keeps the last real measurement. (§4.6, and the same
        // reason alert evaluation is skipped below.) Monitor-outage / unschedulable data points are written
        // straight to the repository by the dispatcher and never reach this path, so they never move it.
        if (aggregatedResult.Status == ServiceStatus.FAILURE)
            return;

        var previousStatus = check.CurrentStatus;
        check.CurrentStatus = aggregatedResult.Status;
        await checkRepo.UpdateAsync(check, ct);

        var evt = new CheckStatusChangedEvent(check.Id, check.ServiceId, previousStatus, aggregatedResult.Status);
        statusChannel.Writer.TryWrite(evt);

        await alertEvaluationService.EvaluateAsync(check.Id, ct);
    }
}
