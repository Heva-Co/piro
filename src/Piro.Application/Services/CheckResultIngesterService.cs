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
        await IngestDataPointOnlyAsync(checkId, result, workerRegion, ct);
        await IngestStatusOnlyAsync(checkId, result, ct);
    }

    // ── Per-region data point persistence (multi-region step 1) ──────────────

    public async Task IngestDataPointOnlyAsync(int checkId, CheckExecutionResult result, string workerRegion, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        timestamp -= timestamp % 60;

        var dataPoint = new CheckDataPoint
        {
            CheckId = checkId,
            Timestamp = timestamp,
            Status = result.Status,
            LatencyMs = result.LatencyMs,
            DataType = DataPointType.REALTIME,
            ErrorMessage = result.ErrorMessage,
            WorkerRegion = workerRegion
        };

        try
        {
            await dataPointRepo.CreateAsync(dataPoint, ct);
        }
        catch (Exception)
        {
            // Duplicate PK (same minute + region) — safe to ignore
        }
    }

    // ── Status update + events + alerts (multi-region step 2, once per batch) ─

    public async Task IngestStatusOnlyAsync(int checkId, CheckExecutionResult aggregatedResult, CancellationToken ct = default)
    {
        var check = await checkRepo.GetByIdAsync(checkId, ct);
        if (check is null) return;

        var previousStatus = check.CurrentStatus;
        check.CurrentStatus = aggregatedResult.Status;
        await checkRepo.UpdateAsync(check, ct);

        var evt = new CheckStatusChangedEvent(check.Id, check.ServiceId, previousStatus, aggregatedResult.Status);
        statusChannel.Writer.TryWrite(evt);

        // FAILURE means the executor itself crashed — not a service outage, so skip alert evaluation.
        if (aggregatedResult.Status != ServiceStatus.FAILURE)
            await alertEvaluationService.EvaluateAsync(check.Id, ct);
    }
}
