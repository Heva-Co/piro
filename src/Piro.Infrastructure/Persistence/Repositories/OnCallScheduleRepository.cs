using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class OnCallScheduleRepository(PiroDbContext db) : IOnCallScheduleRepository
{
    public async Task<List<OnCallSchedule>> GetAllAsync(CancellationToken ct = default) =>
        await db.OnCallSchedules
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<OnCallSchedule?> GetByIdWithLayersAsync(int id, CancellationToken ct = default) =>
        await db.OnCallSchedules
            .Include(s => s.Layers)
                .ThenInclude(l => l.Users)
                    .ThenInclude(u => u.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.ReplacesUser)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<OnCallSchedule> CreateAsync(OnCallSchedule schedule, CancellationToken ct = default)
    {
        db.OnCallSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return schedule;
    }

    public async Task UpdateAsync(OnCallSchedule schedule, CancellationToken ct = default)
    {
        db.OnCallSchedules.Update(schedule);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var schedule = await db.OnCallSchedules.FindAsync([id], ct);
        if (schedule is not null)
        {
            db.OnCallSchedules.Remove(schedule);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<OnCallLayer> CreateLayerAsync(OnCallLayer layer, CancellationToken ct = default)
    {
        db.OnCallLayers.Add(layer);
        await db.SaveChangesAsync(ct);
        // Reload with users
        return await db.OnCallLayers
            .Include(l => l.Users).ThenInclude(u => u.User)
            .FirstAsync(l => l.Id == layer.Id, ct);
    }

    public async Task<OnCallLayer> UpdateLayerAsync(OnCallLayer layer, CancellationToken ct = default)
    {
        // Replace users: remove existing, add new ones
        var existingUsers = await db.OnCallLayerUsers.Where(u => u.LayerId == layer.Id).ToListAsync(ct);
        db.OnCallLayerUsers.RemoveRange(existingUsers);

        db.OnCallLayers.Update(layer);
        await db.SaveChangesAsync(ct);

        return await db.OnCallLayers
            .Include(l => l.Users).ThenInclude(u => u.User)
            .FirstAsync(l => l.Id == layer.Id, ct);
    }

    public async Task DeleteLayerAsync(int layerId, CancellationToken ct = default)
    {
        var layer = await db.OnCallLayers.FindAsync([layerId], ct);
        if (layer is not null)
        {
            db.OnCallLayers.Remove(layer);
            await db.SaveChangesAsync(ct);
        }
    }
}
