using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Singleton that accumulates per-worker results for multi-region checks and
/// triggers exactly one status update + alert evaluation per execution cycle.
///
/// Flow:
/// 1. <c>RemoteCheckJobDispatcher</c> calls <see cref="RegisterBatch"/> before fan-out.
/// 2. <c>WorkerHub.ResultAsync</c> calls <see cref="AddResult"/> for each worker reply.
/// 3. When all N results arrive (or a 60 s timeout fires), <see cref="CompleteAsync"/>
///    aggregates (worst-status wins) and invokes
///    <see cref="ICheckResultIngester.IngestStatusOnlyAsync"/> via a fresh DI scope.
/// </summary>
public sealed class MultiRegionBatchTracker(
    IServiceScopeFactory scopeFactory,
    ILogger<MultiRegionBatchTracker> logger) : IMultiRegionBatchTracker, IDisposable
{
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, BatchState> _batches = new();

    public void RegisterBatch(string batchId, int checkId, int expectedCount)
    {
        var state = new BatchState(checkId, expectedCount);

        // Safety timeout: complete with whatever results arrived by then
        state.TimeoutTimer = new Timer(
            callback: _ => _ = TimeoutAsync(batchId),
            state: null,
            dueTime: BatchTimeout,
            period: Timeout.InfiniteTimeSpan);

        _batches[batchId] = state;

        logger.LogDebug("Batch {BatchId} registered — check {CheckId}, expecting {N} results.",
            batchId, checkId, expectedCount);
    }

    public void AddResult(string batchId, CheckExecutionResult result)
    {
        if (!_batches.TryGetValue(batchId, out var state))
        {
            // Stale result from a batch that already completed or timed out
            logger.LogDebug("Received result for unknown/expired batch {BatchId}. Ignored.", batchId);
            return;
        }

        List<CheckExecutionResult>? completed = null;

        lock (state)
        {
            state.Results.Add(result);
            if (state.Results.Count >= state.ExpectedCount)
                completed = [.. state.Results];
        }

        if (completed is not null)
        {
            if (_batches.TryRemove(batchId, out var removed))
            {
                removed.TimeoutTimer?.Dispose();
                _ = CompleteAsync(removed.CheckId, completed, "all results received");
            }
        }
    }

    private async Task TimeoutAsync(string batchId)
    {
        if (!_batches.TryRemove(batchId, out var state)) return;

        List<CheckExecutionResult> results;
        lock (state) { results = [.. state.Results]; }

        state.TimeoutTimer?.Dispose();

        if (results.Count == 0)
        {
            logger.LogWarning("Batch {BatchId} timed out with zero results — check {CheckId} skipped status update.",
                batchId, state.CheckId);
            return;
        }

        await CompleteAsync(state.CheckId, results, $"timeout after {BatchTimeout.TotalSeconds}s");
    }

    private async Task CompleteAsync(int checkId, List<CheckExecutionResult> results, string reason)
    {
        // Aggregate: worst operational status wins (DOWN > DEGRADED > UP > NO_DATA).
        // Exclude MAINTENANCE — that is set externally, not by execution results.
        var worstStatus = results
            .Select(r => r.Status == ServiceStatus.MAINTENANCE ? ServiceStatus.DOWN : r.Status)
            .OrderByDescending(s => (int)s)
            .First();

        var avgLatency = results.Any(r => r.LatencyMs.HasValue)
            ? results.Where(r => r.LatencyMs.HasValue).Average(r => r.LatencyMs!.Value)
            : (double?)null;

        var firstError = results.FirstOrDefault(r => r.ErrorMessage is not null)?.ErrorMessage;

        var aggregated = new CheckExecutionResult(worstStatus, avgLatency, firstError);

        logger.LogDebug(
            "Batch for check {CheckId} complete ({Reason}). {Count}/{Total} results — aggregated status: {Status}.",
            checkId, reason, results.Count, results.Count, worstStatus);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ingester = scope.ServiceProvider.GetRequiredService<ICheckResultIngester>();
            await ingester.IngestStatusOnlyAsync(checkId, aggregated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete multi-region batch for check {CheckId}.", checkId);
        }
    }

    public void Dispose()
    {
        foreach (var (_, state) in _batches)
            state.TimeoutTimer?.Dispose();
        _batches.Clear();
    }

    // ── Inner state ───────────────────────────────────────────────────────────

    private sealed class BatchState(int checkId, int expectedCount)
    {
        public int CheckId { get; } = checkId;
        public int ExpectedCount { get; } = expectedCount;
        public List<CheckExecutionResult> Results { get; } = [];
        public Timer? TimeoutTimer { get; set; }
    }
}
