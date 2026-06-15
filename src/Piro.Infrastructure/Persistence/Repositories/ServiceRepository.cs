using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IServiceRepository"/>.</summary>
internal class ServiceRepository(PiroDbContext db) : IServiceRepository
{
    public async Task<IEnumerable<Service>> GetAllAsync(CancellationToken ct = default) =>
        await db.Services.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name).ToListAsync(ct);

    public async Task<Service?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await db.Services.FirstOrDefaultAsync(s => s.Slug == slug, ct);

    public async Task<Service?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Services.FindAsync([id], ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        await db.Services.AnyAsync(s => s.Slug == slug, ct);

    public async Task<Service> CreateAsync(Service service, CancellationToken ct = default)
    {
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
        return service;
    }

    public async Task<Service> UpdateAsync(Service service, CancellationToken ct = default)
    {
        db.Services.Update(service);
        await db.SaveChangesAsync(ct);
        return service;
    }

    public async Task DeleteAsync(Service service, CancellationToken ct = default)
    {
        db.Services.Remove(service);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<int>> GetBlockingDependencyIdsAsync(int serviceId, CancellationToken ct = default) =>
        await db.ServiceDependencies
            .Where(d => d.ServiceId == serviceId && d.PropagationMode == Domain.Enums.DependencyPropagationMode.Blocking)
            .Select(d => d.DependsOnServiceId)
            .ToListAsync(ct);
}
