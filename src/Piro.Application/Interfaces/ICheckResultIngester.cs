using Piro.Application.Models;

namespace Piro.Application.Interfaces;

public interface ICheckResultIngester
{
    /// <summary>
    /// Full single-region ingestion: persists the data point, updates <c>CurrentStatus</c>,
    /// fires a <see cref="CheckStatusChangedEvent"/>, and evaluates alert thresholds.
    /// Use this for local (non-multi-region) checks.
    /// </summary>
    Task IngestAsync(int checkId, CheckExecutionResult result, string workerRegion, CancellationToken ct = default);

    /// <summary>
    /// Persists a per-region data point only — does NOT update <c>CurrentStatus</c> or fire alerts.
    /// Called per worker result during multi-region fan-out; <see cref="IngestStatusOnlyAsync"/>
    /// is called exactly once per batch by <see cref="IMultiRegionBatchTracker"/>.
    /// </summary>
    Task IngestDataPointOnlyAsync(int checkId, CheckExecutionResult result, string workerRegion, CancellationToken ct = default);

    /// <summary>
    /// Updates <c>CurrentStatus</c>, fires a <see cref="CheckStatusChangedEvent"/>, and evaluates
    /// alert thresholds using an aggregated result. Called once per multi-region batch.
    /// </summary>
    Task IngestStatusOnlyAsync(int checkId, CheckExecutionResult aggregatedResult, CancellationToken ct = default);
}
