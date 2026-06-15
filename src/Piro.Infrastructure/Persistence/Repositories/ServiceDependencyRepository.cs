using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IServiceDependencyRepository"/>.</summary>
internal class ServiceDependencyRepository(PiroDbContext db) : IServiceDependencyRepository
{
    public async Task<IEnumerable<ServiceDependency>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .Include(d => d.DependsOnService)
            .Where(d => d.ServiceId == serviceId)
            .ToListAsync(ct);

    public async Task<ServiceDependency?> GetAsync(int serviceId, int dependsOnServiceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .FirstOrDefaultAsync(d => d.ServiceId == serviceId && d.DependsOnServiceId == dependsOnServiceId, ct);

    public async Task<bool> ExistsAsync(int serviceId, int dependsOnServiceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .AnyAsync(d => d.ServiceId == serviceId && d.DependsOnServiceId == dependsOnServiceId, ct);

    public async Task<ServiceDependency> CreateAsync(ServiceDependency dependency, CancellationToken ct = default)
    {
        db.ServiceDependencies.Add(dependency);
        await db.SaveChangesAsync(ct);
        return dependency;
    }

    public async Task DeleteAsync(ServiceDependency dependency, CancellationToken ct = default)
    {
        db.ServiceDependencies.Remove(dependency);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<int>> GetDependsOnIdsAsync(int serviceId, DependencyPropagationMode? mode = null, CancellationToken ct = default)
    {
        var query = db.ServiceDependencies.Where(d => d.ServiceId == serviceId);
        if (mode is not null)
            query = query.Where(d => d.PropagationMode == mode);
        return await query.Select(d => d.DependsOnServiceId).ToListAsync(ct);
    }

    public async Task<IEnumerable<ServiceDependency>> GetUpstreamDependenciesAsync(int serviceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .Include(d => d.DependsOnService)
            .Where(d => d.ServiceId == serviceId &&
                        d.PropagationMode != DependencyPropagationMode.Advisory)
            .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetBlockingDownstreamServiceIdsAsync(int serviceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .Where(d => d.DependsOnServiceId == serviceId &&
                        d.PropagationMode != DependencyPropagationMode.Advisory)
            .Select(d => d.ServiceId)
            .ToListAsync(ct);
}
