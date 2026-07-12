using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IServiceRepository"/>.</summary>
internal class ServiceRepository(PiroDbContext db) : IServiceRepository
{
    public async Task<IEnumerable<Service>> GetAllAsync(CancellationToken ct = default) =>
        await db.Services.Include(s => s.EscalationPolicy).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name).ToListAsync(ct);

    public async Task<(IEnumerable<Service> Items, int TotalCount)> GetPagedAsync(ServiceQueryParams query, CancellationToken ct = default)
    {
        var q = db.Services.Include(s => s.EscalationPolicy).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(s => EF.Functions.ILike(s.Name, $"%{search}%") || EF.Functions.ILike(s.Slug, $"%{search}%"));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(1, query.Page);

        var items = await q
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Dictionary<int, int>> GetCheckCountsAsync(CancellationToken ct = default) =>
        await db.Checks
            .GroupBy(c => c.ServiceId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<int> GetCheckCountAsync(int serviceId, CancellationToken ct = default) =>
        await db.Checks.CountAsync(c => c.ServiceId == serviceId, ct);

    public async Task<Service?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await db.Services.Include(s => s.EscalationPolicy).FirstOrDefaultAsync(s => s.Slug == slug, ct);

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
