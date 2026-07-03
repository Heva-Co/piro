using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>Computes and persists the derived status of a service.</summary>
/// <remarks>
/// Evaluates the service's own checks, active incidents, active maintenance windows,
/// then walks Blocking and SoftBlocking upstream dependency edges.
/// Enqueues recomputation for downstream services when the status changes.
/// </remarks>
public class ServiceStatusService(
    IServiceRepository serviceRepo,
    ICheckRepository checkRepo,
    IServiceDependencyRepository dependencyRepo,
    IIncidentRepository incidentRepo)
{
    /// <summary>
    /// Computes the current status for <paramref name="serviceId"/> and returns
    /// the IDs of downstream services that need their status recomputed.
    /// </summary>
    public async Task<IReadOnlyList<int>> ComputeAsync(int serviceId, CancellationToken ct = default)
    {
        var service = await serviceRepo.GetByIdAsync(serviceId, ct);
        if (service is null) return [];

        // 1. Raw status from own checks
        var checks = await checkRepo.GetByServiceIdAsync(serviceId, ct);
        var rawStatus = checks
            .Where(c => c.IsActive)
            .Select(c => c.CurrentStatus)
            .Aggregate(ServiceStatus.NO_DATA, Worst);

        // 2. Maintenance overrides everything — short-circuit
        // (MaintenanceService will be implemented in v0.9; placeholder check here)
        // if (await maintenanceRepo.HasActiveWindowAsync(serviceId, ct))
        //     return await PersistAndCascadeAsync(service, ServiceStatus.MAINTENANCE, [], ct);

        // 3. Active incident impact overrides check status
        var incidentImpact = await incidentRepo.GetActiveImpactForServiceAsync(serviceId, ct);
        if (incidentImpact.HasValue)
            rawStatus = Worst(rawStatus, incidentImpact.Value);

        // 4. Propagation from Blocking/SoftBlocking upstream dependencies
        var upstreamDeps = await dependencyRepo.GetUpstreamDependenciesAsync(serviceId, ct);
        var propagationSources = new List<string>();

        foreach (var dep in upstreamDeps)
        {
            var upstreamStatus = dep.DependsOnService.CurrentStatus;

            if (upstreamStatus is not (ServiceStatus.DOWN or ServiceStatus.DEGRADED))
                continue;

            var impact = dep.PropagationMode == DependencyPropagationMode.SoftBlocking
                ? ServiceStatus.DEGRADED          // cap at DEGRADED
                : upstreamStatus;                 // propagate exact status

            rawStatus = Worst(rawStatus, impact);
            propagationSources.Add(dep.DependsOnService.Slug);
        }

        return await PersistAndCascadeAsync(service, rawStatus, propagationSources, ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<int>> PersistAndCascadeAsync(
        Service service,
        ServiceStatus newStatus,
        IReadOnlyList<string> propagationSources,
        CancellationToken ct)
    {
        var changed = service.CurrentStatus != newStatus;
        service.CurrentStatus = newStatus;
        await serviceRepo.UpdateAsync(service, ct);

        if (!changed) return [];

        // Return downstream service IDs so the caller can enqueue their recomputation
        var downstream = await dependencyRepo.GetBlockingDownstreamServiceIdsAsync(service.Id, ct);
        return downstream.ToList();
    }

    /// <summary>Returns the more severe of two statuses. Order: MAINTENANCE > DOWN > DEGRADED > UP > NO_DATA.</summary>
    private static ServiceStatus Worst(ServiceStatus a, ServiceStatus b) =>
        (int)a > (int)b ? a : b;
}
