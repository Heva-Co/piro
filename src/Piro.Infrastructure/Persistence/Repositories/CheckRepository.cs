using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ICheckRepository"/>.</summary>
internal class CheckRepository(PiroDbContext db) : ICheckRepository
{
    public async Task<IEnumerable<Check>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default) =>
        await db.Checks.Where(c => c.ServiceId == serviceId).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<IEnumerable<Check>> GetAllActiveAsync(CancellationToken ct = default) =>
        await db.Checks.Where(c => c.IsActive).ToListAsync(ct);

    public async Task<IEnumerable<Check>> GetAllWithServiceAsync(CancellationToken ct = default) =>
        await db.Checks.Include(c => c.Service).OrderBy(c => c.Service.Name).ThenBy(c => c.Name).ToListAsync(ct);

    public async Task<IEnumerable<(Check Check, string? LastErrorMessage)>> GetAllWithServiceAndLastErrorAsync(CancellationToken ct = default)
    {
        var rows = await db.Checks
            .Include(c => c.Service)
            .OrderBy(c => c.Service.Name).ThenBy(c => c.Name)
            .Select(c => new
            {
                Check = c,
                LastErrorMessage = db.CheckDataPoints
                    .Where(dp => dp.CheckId == c.Id && dp.ErrorMessage != null)
                    .OrderByDescending(dp => dp.Timestamp)
                    .Select(dp => dp.ErrorMessage)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => (r.Check, r.LastErrorMessage));
    }

    public async Task<Check?> GetBySlugAsync(int serviceId, string slug, CancellationToken ct = default) =>
        await db.Checks.FirstOrDefaultAsync(c => c.ServiceId == serviceId && c.Slug == slug, ct);

    public async Task<Check?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Checks.FindAsync([id], ct);

    public async Task<bool> SlugExistsInServiceAsync(int serviceId, string slug, CancellationToken ct = default) =>
        await db.Checks.AnyAsync(c => c.ServiceId == serviceId && c.Slug == slug, ct);

    public async Task<Check> CreateAsync(Check check, CancellationToken ct = default)
    {
        db.Checks.Add(check);
        await db.SaveChangesAsync(ct);
        return check;
    }

    public async Task<Check> UpdateAsync(Check check, CancellationToken ct = default)
    {
        db.Checks.Update(check);
        await db.SaveChangesAsync(ct);
        return check;
    }

    public async Task DeleteAsync(Check check, CancellationToken ct = default)
    {
        db.Checks.Remove(check);
        await db.SaveChangesAsync(ct);
    }
}
