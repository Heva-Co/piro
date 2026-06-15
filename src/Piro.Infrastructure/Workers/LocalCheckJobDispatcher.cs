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
    string workerRegion) : ICheckJobDispatcher
{
    private readonly Dictionary<CheckType, ICheckExecutor> _executors =
        executors.ToDictionary(e => e.CheckType);

    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        if (!_executors.TryGetValue(check.Type, out var executor))
            return; // no executor registered for this type yet

        var result = await executor.ExecuteAsync(check, ct);
        await ingester.IngestAsync(check.Id, result, workerRegion, ct);
    }
}
