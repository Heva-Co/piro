using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="ServiceDependency"/> DAG edges.</summary>
public interface IServiceDependencyRepository
{
    /// <summary>Returns all dependencies (edges) declared by the given service.</summary>
    Task<IEnumerable<ServiceDependency>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default);

    Task<ServiceDependency?> GetAsync(int serviceId, int dependsOnServiceId, CancellationToken ct = default);

    Task<bool> ExistsAsync(int serviceId, int dependsOnServiceId, CancellationToken ct = default);

    Task<ServiceDependency> CreateAsync(ServiceDependency dependency, CancellationToken ct = default);

    Task DeleteAsync(ServiceDependency dependency, CancellationToken ct = default);

    /// <summary>
    /// Returns all service IDs that the given service directly depends on,
    /// filtered by propagation mode. Used by the cycle detection algorithm.
    /// </summary>
    Task<IEnumerable<int>> GetDependsOnIdsAsync(int serviceId, DependencyPropagationMode? mode = null, CancellationToken ct = default);

    /// <summary>
    /// Returns all upstream dependencies (with their mode) for a service.
    /// Used by the status propagation algorithm.
    /// </summary>
    Task<IEnumerable<ServiceDependency>> GetUpstreamDependenciesAsync(int serviceId, CancellationToken ct = default);

    /// <summary>
    /// Returns IDs of services that have a Blocking or SoftBlocking dependency on the given service.
    /// Used to cascade recomputation downstream after a status change.
    /// </summary>
    Task<IEnumerable<int>> GetBlockingDownstreamServiceIdsAsync(int serviceId, CancellationToken ct = default);
}
