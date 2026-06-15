using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ICheckDataPointRepository"/>.</summary>
internal class CheckDataPointRepository(PiroDbContext db) : ICheckDataPointRepository
{
    public async Task<IEnumerable<CheckDataPoint>> GetByCheckIdAsync(
        int checkId, long? from = null, long? to = null, CancellationToken ct = default)
    {
        var query = db.CheckDataPoints.Where(p => p.CheckId == checkId);
        if (from.HasValue) query = query.Where(p => p.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(p => p.Timestamp <= to.Value);
        return await query.OrderByDescending(p => p.Timestamp).ToListAsync(ct);
    }

    public async Task CreateAsync(CheckDataPoint dataPoint, CancellationToken ct = default)
    {
        db.CheckDataPoints.Add(dataPoint);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Detach so the failed entity doesn't poison subsequent SaveChangesAsync calls
            db.Entry(dataPoint).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            throw;
        }
    }

    public async Task<IEnumerable<(long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByServiceIdAsync(
        int serviceId, long from, long to, CancellationToken ct = default)
    {
        var rows = await db.CheckDataPoints
            .Where(p => p.Check.ServiceId == serviceId && p.Timestamp >= from && p.Timestamp <= to && p.LatencyMs.HasValue)
            .Select(p => new { p.Timestamp, p.LatencyMs })
            .ToListAsync(ct);

        return rows
            .GroupBy(p => p.Timestamp / 86400)
            .Select(g => (
                DayTimestamp: g.Key * 86400,
                Avg: g.Average(p => p.LatencyMs!.Value),
                Min: g.Min(p => p.LatencyMs!.Value),
                Max: g.Max(p => p.LatencyMs!.Value)
            ))
            .OrderBy(d => d.DayTimestamp)
            .ToList();
    }

    public async Task<CheckDataPoint?> GetLatestByServiceIdAsync(int serviceId, CancellationToken ct = default)
    {
        return await db.CheckDataPoints
            .Where(p => p.Check.ServiceId == serviceId)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<(string Region, long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByRegionAsync(
        int checkId, long from, long to, CancellationToken ct = default)
    {
        var rows = await db.CheckDataPoints
            .Where(p => p.CheckId == checkId && p.Timestamp >= from && p.Timestamp <= to && p.LatencyMs.HasValue)
            .Select(p => new { p.WorkerRegion, p.Timestamp, p.LatencyMs })
            .ToListAsync(ct);

        return rows
            .GroupBy(p => (p.WorkerRegion, Day: p.Timestamp / 86400))
            .Select(g => (
                Region: g.Key.WorkerRegion,
                DayTimestamp: g.Key.Day * 86400,
                Avg: g.Average(p => p.LatencyMs!.Value),
                Min: g.Min(p => p.LatencyMs!.Value),
                Max: g.Max(p => p.LatencyMs!.Value)
            ))
            .OrderBy(d => d.Region).ThenBy(d => d.DayTimestamp)
            .ToList();
    }
}
