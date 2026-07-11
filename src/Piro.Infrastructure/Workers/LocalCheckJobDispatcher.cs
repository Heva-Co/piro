using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Executes checks in-process using registered <see cref="ICheckExecutor"/>
/// implementations and immediately ingests results via <see cref="ICheckResultIngester"/>.
/// </summary>
/// <remarks>
/// This is the Phase 1/2 implementation. Phase 3 will introduce
/// <c>RemoteCheckJobDispatcher</c> which forwards checks to regional SignalR workers.
/// </remarks>
internal class LocalCheckJobDispatcher(
    IEnumerable<ICheckExecutor> executors,
    ICheckResultIngester ingester,
    ICheckDataPointRepository dataPointRepo,
    string workerRegion,
    ILogger<LocalCheckJobDispatcher> logger) : ICheckJobDispatcher
{
    private readonly Dictionary<CheckType, ICheckExecutor> _executors =
        executors.ToDictionary(e => e.CheckType);

    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        if (!_executors.TryGetValue(check.Type, out var executor))
        {
            logger.LogWarning(
                "No executor registered for check type {CheckType}. Check {CheckId} skipped — writing MONITOR_OUTAGE datapoint.",
                check.Type, check.Id);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            timestamp -= timestamp % 60;

            var gapPoint = new CheckDataPoint
            {
                CheckId = check.Id,
                Timestamp = timestamp,
                Status = ServiceStatus.NO_DATA,
                DataType = DataPointType.MONITOR_OUTAGE,
                WorkerRegion = workerRegion,
                ErrorMessage = $"No executor registered for check type {check.Type}"
            };

            await dataPointRepo.CreateAsync(gapPoint, ct);
            return;
        }

        var result = await executor.ExecuteAsync(check, ct);
        await ingester.IngestAsync(check.Id, result, workerRegion, ct);
    }
}
