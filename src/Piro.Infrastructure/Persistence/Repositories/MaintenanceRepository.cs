using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IMaintenanceRepository"/>.</summary>
public class MaintenanceRepository(PiroDbContext db) : IMaintenanceRepository
{
    public async Task<IEnumerable<Maintenance>> GetAllAsync(CancellationToken ct = default) =>
        await db.Maintenances
            .Include(m => m.Events.Where(e => e.Status != MaintenanceEventStatus.Completed).OrderBy(e => e.StartDateTime))
            .Include(m => m.MaintenanceServices).ThenInclude(ms => ms.Service)
            .OrderByDescending(m => m.StartDateTime)
            .ToListAsync(ct);

    public async Task<Maintenance?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Maintenances
            .Include(m => m.Events.OrderBy(e => e.StartDateTime))
            .Include(m => m.MaintenanceServices).ThenInclude(ms => ms.Service)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IEnumerable<Maintenance>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Maintenances
            .Where(m => m.Status == MaintenanceStatus.Active)
            .ToListAsync(ct);

    public async Task<Maintenance> CreateAsync(Maintenance maintenance, CancellationToken ct = default)
    {
        db.Maintenances.Add(maintenance);
        await db.SaveChangesAsync(ct);
        return maintenance;
    }

    public async Task<Maintenance> UpdateAsync(Maintenance maintenance, CancellationToken ct = default)
    {
        db.Maintenances.Update(maintenance);
        await db.SaveChangesAsync(ct);
        return maintenance;
    }

    public async Task DeleteAsync(Maintenance maintenance, CancellationToken ct = default)
    {
        db.Maintenances.Remove(maintenance);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddEventsAsync(IEnumerable<MaintenanceEvent> events, CancellationToken ct = default)
    {
        db.MaintenanceEvents.AddRange(events);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFutureEventsAsync(int maintenanceId, long fromTimestamp, CancellationToken ct = default)
    {
        var events = await db.MaintenanceEvents
            .Where(e => e.MaintenanceId == maintenanceId
                     && e.StartDateTime >= fromTimestamp
                     && e.Status == MaintenanceEventStatus.Scheduled)
            .ToListAsync(ct);

        db.MaintenanceEvents.RemoveRange(events);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<MaintenanceEvent>> GetActiveEventsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await db.MaintenanceEvents
            .Where(e => e.Status != MaintenanceEventStatus.Completed
                     && e.Status != MaintenanceEventStatus.Cancelled
                     && e.StartDateTime <= now + 86400  // within next 24h or already started
                     && e.EndDateTime >= now)
            .Include(e => e.Maintenance).ThenInclude(m => m.MaintenanceServices)
            .ToListAsync(ct);
    }

    public async Task<bool> HasActiveWindowAsync(int serviceId, CancellationToken ct = default) =>
        await db.MaintenanceEvents
            .Where(e => e.Status == MaintenanceEventStatus.Ongoing)
            .AnyAsync(e => e.Maintenance.IsGlobal || e.Maintenance.MaintenanceServices.Any(ms => ms.ServiceId == serviceId), ct);

    public async Task<IReadOnlyList<int>> GetAffectedServiceIdsAsync(int maintenanceId, CancellationToken ct = default)
    {
        var maintenance = await db.Maintenances
            .Include(m => m.MaintenanceServices)
            .FirstOrDefaultAsync(m => m.Id == maintenanceId, ct);
        if (maintenance is null) return [];

        if (maintenance.IsGlobal)
            return await db.Services.Select(s => s.Id).ToListAsync(ct);

        return maintenance.MaintenanceServices.Select(ms => ms.ServiceId).ToList();
    }

    public async Task<MaintenanceEvent?> GetEventByIdAsync(int maintenanceId, int eventId, CancellationToken ct = default)
    {
        return await db.MaintenanceEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.MaintenanceId == maintenanceId, ct);
    }

    public async Task CancelEventAsync(MaintenanceEvent maintenanceEvent, CancellationToken ct = default)
    {
        maintenanceEvent.Status = MaintenanceEventStatus.Cancelled;
        db.MaintenanceEvents.Update(maintenanceEvent);
        await db.SaveChangesAsync(ct);
    }
}
