using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ICheckDataPointRepository"/>.</summary>
internal class CheckDataPointRepository(PiroDbContext db, ILogger<CheckDataPointRepository> logger) : ICheckDataPointRepository
{
    public async Task<IEnumerable<CheckDataPoint>> GetByCheckIdAsync(
        int checkId, long? from = null, long? to = null, string? region = null, int? limit = null, CancellationToken ct = default)
    {
        var query = db.CheckDataPoints.Where(p => p.CheckId == checkId);
        if (from.HasValue) query = query.Where(p => p.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(p => p.Timestamp <= to.Value);
        if (region is not null) query = query.Where(p => p.WorkerRegion == region);

        query = query.OrderByDescending(p => p.Timestamp);
        if (limit.HasValue) query = query.Take(limit.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<bool> CreateAsync(CheckDataPoint dataPoint, CancellationToken ct = default)
    {
        db.CheckDataPoints.Add(dataPoint);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Duplicate PK (same check/minute/region) — safe to ignore, another writer already has this row
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist data point for check {CheckId} at {Timestamp}.", dataPoint.CheckId, dataPoint.Timestamp);
            throw;
        }
        finally
        {
            // Detach so a failed entity doesn't poison subsequent SaveChangesAsync calls
            if (db.Entry(dataPoint).State != EntityState.Detached)
                db.Entry(dataPoint).State = EntityState.Detached;
        }
    }

    public async Task<IEnumerable<(long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByServiceIdAsync(
        int serviceId, long from, long to, CancellationToken ct = default)
    {
        // Latency lives in the jsonb Dimensions column now, so materialize the map and read it in memory
        // rather than projecting a NotMapped property EF can't translate.
        var rows = await db.CheckDataPoints
            .Where(p => p.Check.ServiceId == serviceId && p.Timestamp >= from && p.Timestamp <= to)
            .Select(p => new { p.Timestamp, p.Dimensions })
            .ToListAsync(ct);

        return rows
            .Select(p => new { p.Timestamp, Latency = Latency(p.Dimensions) })
            .Where(p => p.Latency.HasValue)
            .GroupBy(p => p.Timestamp / 86400)
            .Select(g => (
                DayTimestamp: g.Key * 86400,
                Avg: g.Average(p => p.Latency!.Value),
                Min: g.Min(p => p.Latency!.Value),
                Max: g.Max(p => p.Latency!.Value)
            ))
            .OrderBy(d => d.DayTimestamp)
            .ToList();
    }

    public async Task<IEnumerable<CheckDailyStats>> GetDailyStatsByCheckIdAsync(
        int checkId, long from, long to, CancellationToken ct = default)
    {
        var rows = await db.CheckDataPoints
            .Where(p => p.CheckId == checkId && p.Timestamp >= from && p.Timestamp <= to)
            .Select(p => new { p.WorkerRegion, p.Timestamp, p.Status, p.Dimensions })
            .ToListAsync(ct);

        return rows
            .Select(p => new { p.WorkerRegion, p.Timestamp, p.Status, Latency = Latency(p.Dimensions) })
            .GroupBy(p => (p.WorkerRegion, Day: p.Timestamp / 86400))
            .Select(g => new CheckDailyStats(
                Region: g.Key.WorkerRegion,
                DayTimestamp: g.Key.Day * 86400,
                CountUp: g.Count(p => p.Status == ServiceStatus.UP),
                CountDown: g.Count(p => p.Status == ServiceStatus.DOWN || p.Status == ServiceStatus.FAILURE),
                CountDegraded: g.Count(p => p.Status == ServiceStatus.DEGRADED),
                AvgLatencyMs: g.Any(p => p.Latency.HasValue)
                    ? g.Where(p => p.Latency.HasValue).Average(p => p.Latency!.Value)
                    : null))
            .OrderBy(d => d.Region).ThenBy(d => d.DayTimestamp)
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
            .Where(p => p.CheckId == checkId && p.Timestamp >= from && p.Timestamp <= to)
            .Select(p => new { p.WorkerRegion, p.Timestamp, p.Dimensions })
            .ToListAsync(ct);

        return rows
            .Select(p => new { p.WorkerRegion, p.Timestamp, Latency = Latency(p.Dimensions) })
            .Where(p => p.Latency.HasValue)
            .GroupBy(p => (p.WorkerRegion, Day: p.Timestamp / 86400))
            .Select(g => (
                Region: g.Key.WorkerRegion,
                DayTimestamp: g.Key.Day * 86400,
                Avg: g.Average(p => p.Latency!.Value),
                Min: g.Min(p => p.Latency!.Value),
                Max: g.Max(p => p.Latency!.Value)
            ))
            .OrderBy(d => d.Region).ThenBy(d => d.DayTimestamp)
            .ToList();
    }

    private static double? Latency(Dictionary<string, double> dimensions) =>
        dimensions.TryGetValue("Latency", out var v) ? v : null;
}
