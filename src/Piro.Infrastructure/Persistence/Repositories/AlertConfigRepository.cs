using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAlertConfigRepository"/>.</summary>
internal class AlertConfigRepository(PiroDbContext db) : IAlertConfigRepository
{
    public async Task<IEnumerable<AlertConfig>> GetAllAsync(CancellationToken ct = default) =>
        await db.AlertConfigs
            .Include(a => a.AlertConfigTriggers)
            .ToListAsync(ct);

    public async Task<IEnumerable<AlertConfig>> GetByCheckIdAsync(int checkId, CancellationToken ct = default) =>
        await db.AlertConfigs
            .Include(a => a.AlertConfigTriggers)
                .ThenInclude(act => act.Trigger)
            .Where(a => a.CheckId == checkId)
            .ToListAsync(ct);

    public async Task<AlertConfig?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.AlertConfigs
            .Include(a => a.AlertConfigTriggers)
                .ThenInclude(act => act.Trigger)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AlertConfig> CreateAsync(AlertConfig config, CancellationToken ct = default)
    {
        db.AlertConfigs.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public async Task<AlertConfig> UpdateAsync(AlertConfig config, CancellationToken ct = default)
    {
        db.AlertConfigs.Update(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public async Task DeleteAsync(AlertConfig config, CancellationToken ct = default)
    {
        db.AlertConfigs.Remove(config);
        await db.SaveChangesAsync(ct);
    }
}
