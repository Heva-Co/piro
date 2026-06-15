using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IServiceStatusSnapshotRepository"/>.</summary>
internal class ServiceStatusSnapshotRepository(PiroDbContext db) : IServiceStatusSnapshotRepository
{
    public async Task UpsertAsync(ServiceStatusSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await db.ServiceStatusSnapshots
            .FirstOrDefaultAsync(s => s.ServiceId == snapshot.ServiceId && s.Timestamp == snapshot.Timestamp, ct);

        if (existing is null)
            db.ServiceStatusSnapshots.Add(snapshot);
        else
        {
            existing.ComputedStatus = snapshot.ComputedStatus;
            existing.PropagationSources = snapshot.PropagationSources;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ServiceStatusSnapshot>> GetByServiceIdAsync(
        int serviceId, long? from = null, long? to = null, CancellationToken ct = default)
    {
        var query = db.ServiceStatusSnapshots.Where(s => s.ServiceId == serviceId);
        if (from.HasValue) query = query.Where(s => s.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(s => s.Timestamp <= to.Value);
        return await query.OrderByDescending(s => s.Timestamp).ToListAsync(ct);
    }

    public async Task<IEnumerable<(long DayTimestamp, int CountUp, int CountDown, int CountDegraded, int CountMaintenance)>> GetDailyCountsAsync(
        int serviceId, long from, long to, CancellationToken ct = default)
    {
        var rows = await db.ServiceStatusSnapshots
            .Where(s => s.ServiceId == serviceId && s.Timestamp >= from && s.Timestamp <= to)
            .Select(s => new { s.Timestamp, s.ComputedStatus })
            .ToListAsync(ct);

        return rows
            .GroupBy(s => s.Timestamp / 86400)
            .Select(g => (
                DayTimestamp: g.Key * 86400,
                CountUp: g.Count(s => s.ComputedStatus == ServiceStatus.UP),
                CountDown: g.Count(s => s.ComputedStatus == ServiceStatus.DOWN),
                CountDegraded: g.Count(s => s.ComputedStatus == ServiceStatus.DEGRADED),
                CountMaintenance: g.Count(s => s.ComputedStatus == ServiceStatus.MAINTENANCE)
            ))
            .OrderBy(d => d.DayTimestamp)
            .ToList();
    }
}
