using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Executes checks in-process through the single registry-backed <see cref="ICheckExecutor"/> and
/// immediately ingests the result via <see cref="ICheckResultIngester"/>. Selection of which check runs
/// is the executor's job (it resolves the check from the registry by type), so this dispatcher no longer
/// keeps a per-type map.
/// </summary>
internal class LocalCheckJobDispatcher(
    ICheckExecutor executor,
    ICheckResultIngester ingester,
    string workerRegion) : ICheckJobDispatcher
{
    public async Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        var result = await executor.ExecuteAsync(check, ct);
        await ingester.IngestAsync(check.Id, result, workerRegion, ct);
    }
}
