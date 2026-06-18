using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Routes each check to the appropriate dispatcher based on <see cref="Check.IsMultiRegion"/>:
/// <list type="bullet">
///   <item><see langword="false"/> — <see cref="LocalCheckJobDispatcher"/>: in-process, embedded worker.</item>
///   <item><see langword="true"/>  — <see cref="RemoteCheckJobDispatcher"/>: fan-out to all connected workers.</item>
/// </list>
/// </summary>
internal class RoutingCheckJobDispatcher(
    LocalCheckJobDispatcher local,
    RemoteCheckJobDispatcher remote,
    IWorkerRegistry registry) : ICheckJobDispatcher
{
    public Task DispatchAsync(Check check, CancellationToken ct = default)
    {
        // Built-in API worker is active when it has a live registry entry
        var apiIsWorker = registry.GetByConnectionId(ApiWorkerHostedService.ApiWorkerConnectionId) is not null;
        if (apiIsWorker)
            return check.IsMultiRegion ? remote.DispatchAsync(check, ct) : local.DispatchAsync(check, ct);
        return check.IsMultiRegion ? remote.DispatchAsync(check, ct) : remote.DispatchToDefaultWorkerAsync(check, ct);
    }
}
