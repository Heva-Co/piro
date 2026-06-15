using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITriggerRepository"/>.</summary>
internal class TriggerRepository(PiroDbContext db) : ITriggerRepository
{
    public async Task<IEnumerable<Trigger>> GetAllAsync(CancellationToken ct = default) =>
        await db.Triggers
            .Include(t => t.AlertConfigTriggers)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IEnumerable<Trigger>> GetGlobalAsync(CancellationToken ct = default) =>
        await db.Triggers.Where(t => t.IsGlobal).ToListAsync(ct);

    public async Task<Trigger?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Triggers.FindAsync([id], ct);

    public async Task<Trigger> CreateAsync(Trigger trigger, CancellationToken ct = default)
    {
        db.Triggers.Add(trigger);
        await db.SaveChangesAsync(ct);
        return trigger;
    }

    public async Task<Trigger> UpdateAsync(Trigger trigger, CancellationToken ct = default)
    {
        db.Triggers.Update(trigger);
        await db.SaveChangesAsync(ct);
        return trigger;
    }

    public async Task DeleteAsync(Trigger trigger, CancellationToken ct = default)
    {
        db.Triggers.Remove(trigger);
        await db.SaveChangesAsync(ct);
    }
}
