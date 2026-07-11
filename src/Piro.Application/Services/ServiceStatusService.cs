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
    IIncidentRepository incidentRepo,
    IMaintenanceRepository maintenanceRepo)
{
    /// <summary>
    /// Computes the current status for <paramref name="serviceId"/> and returns
    /// the IDs of downstream services that need their status recomputed.
    /// </summary>
    public async Task<IReadOnlyList<int>> ComputeAsync(int serviceId, CancellationToken ct = default)
    {
        var service = await serviceRepo.GetByIdAsync(serviceId, ct);
        if (service is null) return [];

        var hasMaintenanceWindow = await maintenanceRepo.HasActiveWindowAsync(serviceId, ct);
        var incidentImpact = await incidentRepo.GetActiveImpactForServiceAsync(serviceId, ct);

        // Public status: defaults to UP, only ever worsened by maintenance or a Public incident's
        // declared impact. Raw check failures never surface here on their own.
        var publicStatus = hasMaintenanceWindow ? ServiceStatus.MAINTENANCE : ServiceStatus.UP;
        if (incidentImpact.HasValue)
            publicStatus = Worst(publicStatus, incidentImpact.Value);

        // 1. Maintenance overrides everything — short-circuit
        if (hasMaintenanceWindow)
            return await PersistAndCascadeAsync(service, ServiceStatus.MAINTENANCE, publicStatus, [], ct);

        // 2. Raw status from own checks
        var checks = await checkRepo.GetByServiceIdAsync(serviceId, ct);
        var rawStatus = checks
            .Where(c => c.IsActive)
            .Select(c => c.CurrentStatus)
            .Aggregate(ServiceStatus.NO_DATA, Worst);

        // 3. Active incident impact overrides check status
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

        return await PersistAndCascadeAsync(service, rawStatus, publicStatus, propagationSources, ct);
    }

    /// <summary>
    /// Computes status for every service in <paramref name="rootServiceIds"/> and cascades
    /// to downstream services until no further status changes occur. Each service is
    /// recomputed at most once per pass; duplicate work across overlapping cascades is skipped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Do not call this directly from controllers, jobs, or other application services.</b>
    /// It performs a read-modify-write on <see cref="Service.CurrentStatus"/> with no optimistic
    /// concurrency guard. If two callers invoke it for the same service at nearly the same time —
    /// e.g. a check finishing execution while a maintenance window transitions, or an admin
    /// clicking "recompute all" mid-way through another recomputation — both read the same stale
    /// status, and whichever writes last silently overwrites the other's result, even if the
    /// other result was the more correct/recent one. No exception is thrown; the wrong status just
    /// sticks until something else happens to trigger another recompute.
    /// </para>
    /// <para>
    /// The only place this is safe to call is <c>StatusDrainHostedService</c>, which is the single
    /// consumer of the <c>Channel&lt;CheckStatusChangedEvent&gt;</c> queue and therefore processes
    /// one service recomputation at a time — that serialization is what actually prevents the race,
    /// not this method. Every other caller (controllers, Quartz jobs, application services) must
    /// enqueue a <c>CheckStatusChangedEvent</c> onto that channel instead of calling this method or
    /// <see cref="ComputeAsync"/> directly. See <c>DependencyService.TriggerRecompute</c> for the
    /// established pattern (a synthetic event with <c>CheckId = 0</c>).
    /// </para>
    /// </remarks>
    [Obsolete("Call sites outside StatusDrainHostedService race concurrently with the channel consumer and can silently overwrite a more recent status. Enqueue a CheckStatusChangedEvent on the shared channel instead — see DependencyService.TriggerRecompute.", error: false)]
    public async Task ComputeAllWithCascadeAsync(IEnumerable<int> rootServiceIds, CancellationToken ct = default)
    {
        var queue = new Queue<int>(rootServiceIds);
        var visited = new HashSet<int>();

        while (queue.Count > 0)
        {
            var serviceId = queue.Dequeue();
            if (!visited.Add(serviceId)) continue;

            var downstream = await ComputeAsync(serviceId, ct);
            foreach (var id in downstream)
                if (!visited.Contains(id))
                    queue.Enqueue(id);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<int>> PersistAndCascadeAsync(
        Service service,
        ServiceStatus newStatus,
        ServiceStatus newPublicStatus,
        IReadOnlyList<string> propagationSources,
        CancellationToken ct)
    {
        var changed = service.CurrentStatus != newStatus;
        service.CurrentStatus = newStatus;
        service.PublicStatus = newPublicStatus;
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
