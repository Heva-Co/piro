using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.Worker;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Hubs;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Fans out a check to every currently connected remote worker simultaneously.
/// Each worker executes independently and returns its own result, enabling
/// multi-region latency comparison and regional outage detection.
///
/// A unique <c>BatchId</c> is generated per execution cycle so <see cref="MultiRegionBatchTracker"/>
/// can accumulate all N worker results and call <c>IngestStatusOnlyAsync</c> exactly once.
///
/// If <c>PIRO_API_WORKER=true</c> is set, the API itself also participates as a local worker
/// and its result is included in the multi-region batch.
///
/// If no workers are connected (and the API is not configured as a worker), a <c>MONITOR_OUTAGE</c>
/// data point is written so the UI can distinguish monitoring gaps from real service outages.
/// </summary>
internal class RemoteCheckJobDispatcher(
    IHubContext<WorkerHub, IWorkerClient> hubContext,
    IWorkerRegistry registry,
    IMultiRegionBatchTracker batchTracker,
    ICheckDataPointRepository dataPointRepo,
    IEnumerable<ICheckExecutor> executors,
    ICheckResultIngester ingester,
    bool apiIsWorker,
    string localWorkerRegion,
    ILogger<RemoteCheckJobDispatcher> logger) : ICheckJobDispatcher
{
    private readonly Dictionary<CheckType, ICheckExecutor> _executors = executors.ToDictionary(e => e.CheckType);

    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        var workers = registry.GetAll();
        var totalCount = workers.Count + (apiIsWorker ? 1 : 0);

        if (totalCount == 0)
        {
            logger.LogWarning(
                "No remote workers connected. Multi-region check {CheckId} skipped — writing MONITOR_OUTAGE datapoint.",
                check.Id);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            timestamp -= timestamp % 60;

            var gapPoint = new CheckDataPoint
            {
                CheckId = check.Id,
                Timestamp = timestamp,
                Status = ServiceStatus.NO_DATA,
                DataType = DataPointType.MONITOR_OUTAGE,
                WorkerRegion = "monitor",
                ErrorMessage = "No remote workers connected"
            };

            try { await dataPointRepo.CreateAsync(gapPoint, ct); }
            catch { /* duplicate for this minute — ignore */ }

            return;
        }

        var batchId = Guid.NewGuid().ToString("N");
        batchTracker.RegisterBatch(batchId, check.Id, totalCount);

        // Fan-out: same check dispatched to every connected remote worker
        var remoteTasks = workers.Select(worker =>
        {
            var message = new WorkerExecuteMessage(
                JobId: Guid.NewGuid().ToString(),
                CheckId: check.Id,
                CheckType: check.Type,
                TypeDataJson: check.TypeDataJson,
                BatchId: batchId);

            logger.LogDebug(
                "Dispatching multi-region check {CheckId} to worker {WorkerId} (region={Region}, batch={BatchId}).",
                check.Id, worker.WorkerId, worker.Region, batchId);

            return hubContext.Clients.Client(worker.ConnectionId).Execute(message);
        });

        var allTasks = new List<Task>(remoteTasks.Cast<Task>());

        // API participates as a local worker — run in-process and feed result into the batch
        if (apiIsWorker && _executors.TryGetValue(check.Type, out var executor))
        {
            allTasks.Add(RunLocalWorkerAsync(executor, check, batchId, ct));
        }
        else if (apiIsWorker)
        {
            // No executor for this check type — count it as a result anyway to avoid hanging the batch
            batchTracker.AddResult(batchId, new CheckExecutionResult(ServiceStatus.NO_DATA, null, "No executor for check type"));
        }

        await Task.WhenAll(allTasks);
    }

    /// <summary>
    /// Routes a non-multi-region check to the single worker marked as default.
    /// Writes a MONITOR_OUTAGE data point if no default worker is connected.
    /// </summary>
    public async Task DispatchToDefaultWorkerAsync(Check check, CancellationToken ct = default)
    {
        var defaultWorker = registry.GetDefaultWorker();
        if (defaultWorker is null)
        {
            logger.LogWarning(
                "No default worker connected. Check {CheckId} skipped — writing MONITOR_OUTAGE datapoint.", check.Id);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            timestamp -= timestamp % 60;

            var gapPoint = new CheckDataPoint
            {
                CheckId = check.Id,
                Timestamp = timestamp,
                Status = ServiceStatus.NO_DATA,
                DataType = DataPointType.MONITOR_OUTAGE,
                WorkerRegion = "monitor",
                ErrorMessage = "No default worker connected"
            };

            try { await dataPointRepo.CreateAsync(gapPoint, ct); }
            catch { /* duplicate for this minute — ignore */ }

            return;
        }

        var message = new WorkerExecuteMessage(
            JobId: Guid.NewGuid().ToString(),
            CheckId: check.Id,
            CheckType: check.Type,
            TypeDataJson: check.TypeDataJson,
            BatchId: null);

        logger.LogDebug(
            "Dispatching check {CheckId} to default worker {WorkerId} (region={Region}).",
            check.Id, defaultWorker.WorkerId, defaultWorker.Region);

        await hubContext.Clients.Client(defaultWorker.ConnectionId).Execute(message);
    }

    private async Task RunLocalWorkerAsync(ICheckExecutor executor, Check check, string batchId, CancellationToken ct)
    {
        try
        {
            var result = await executor.ExecuteAsync(check, ct);
            await ingester.IngestDataPointOnlyAsync(check.Id, result, localWorkerRegion, ct);
            batchTracker.AddResult(batchId, result);

            logger.LogDebug(
                "Local API worker result for check {CheckId} (region={Region}, batch={BatchId}): {Status}.",
                check.Id, localWorkerRegion, batchId, result.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local API worker failed for check {CheckId}.", check.Id);
            batchTracker.AddResult(batchId, new CheckExecutionResult(ServiceStatus.NO_DATA, null, ex.Message));
        }
    }
}
