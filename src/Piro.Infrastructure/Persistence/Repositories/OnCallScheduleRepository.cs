using Microsoft.EntityFrameworkCore;
using Npgsql;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class OnCallScheduleRepository(PiroDbContext db) : IOnCallScheduleRepository
{
    public async Task<(IEnumerable<OnCallSchedule> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var total = await db.OnCallSchedules.CountAsync(ct);
        var clampedPageSize = Math.Clamp(pageSize, 10, 200);
        var clampedPage = Math.Max(1, page);

        var items = await db.OnCallSchedules
            .AsSplitQuery()
            .Include(s => s.Layers)
                .ThenInclude(l => l.Users)
                    .ThenInclude(u => u.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.ReplacesUser)
            .OrderBy(s => s.Name)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<OnCallScheduleMembersDto>> GetAllWithMembersAsync(CancellationToken ct = default)
    {
        var schedules = await db.OnCallSchedules
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                Members = s.Layers
                    .SelectMany(l => l.Users)
                    .Select(u => new { u.UserId, u.User.Name, u.User.Color })
                    .Distinct()
                    .ToList(),
            })
            .ToListAsync(ct);

        return schedules.Select(s => new OnCallScheduleMembersDto(
            s.Id, s.Name,
            s.Members.Select(m => new OnCallMemberDto(m.UserId, m.Name, GetInitials(m.Name), m.Color)).ToList()
        )).ToList();
    }

    private static string GetInitials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    public async Task<OnCallSchedule?> GetByIdWithLayersAsync(int id, CancellationToken ct = default) =>
        await db.OnCallSchedules
            .AsSplitQuery()
            .Include(s => s.Layers)
                .ThenInclude(l => l.Users)
                    .ThenInclude(u => u.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.ReplacesUser)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<OnCallSchedule>> GetSchedulesForUserAsync(int userId, CancellationToken ct = default) =>
        await db.OnCallSchedules
            .AsSplitQuery()
            .Where(s =>
                s.Layers.Any(l => l.Users.Any(u => u.UserId == userId)) ||
                s.Overrides.Any(o => o.UserId == userId || o.ReplacesUserId == userId))
            .Include(s => s.Layers)
                .ThenInclude(l => l.Users)
                    .ThenInclude(u => u.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.User)
            .Include(s => s.Overrides)
                .ThenInclude(o => o.ReplacesUser)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

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
        if (schedule is null) return;

        db.OnCallSchedules.Remove(schedule);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new DomainValidationException("This schedule is referenced by an escalation policy and cannot be deleted.");
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

    public async Task<OnCallOverride> CreateOverrideAsync(OnCallOverride ov, CancellationToken ct = default)
    {
        db.OnCallOverrides.Add(ov);
        await db.SaveChangesAsync(ct);
        return await db.OnCallOverrides
            .Include(o => o.User)
            .Include(o => o.ReplacesUser)
            .FirstAsync(o => o.Id == ov.Id, ct);
    }

    public async Task DeleteOverrideAsync(int overrideId, CancellationToken ct = default)
    {
        var ov = await db.OnCallOverrides.FindAsync([overrideId], ct);
        if (ov is not null)
        {
            db.OnCallOverrides.Remove(ov);
            await db.SaveChangesAsync(ct);
        }
    }
}
