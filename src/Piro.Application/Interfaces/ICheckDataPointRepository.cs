using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="CheckDataPoint"/> time-series records.</summary>
public interface ICheckDataPointRepository
{
    /// <summary>Returns data points for a check, newest first.</summary>
    /// <param name="checkId">The check to fetch data points for.</param>
    /// <param name="from">Inclusive lower bound (unix seconds). Null means unbounded.</param>
    /// <param name="to">Inclusive upper bound (unix seconds). Null means unbounded.</param>
    /// <param name="region">When set, restricts results to this worker region.</param>
    /// <param name="limit">When set, caps the number of rows returned (applied in SQL).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<CheckDataPoint>> GetByCheckIdAsync(
        int checkId, long? from = null, long? to = null, string? region = null, int? limit = null, CancellationToken ct = default);

    /// <summary>Inserts a data point.</summary>
    /// <param name="dataPoint">The data point to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>False if the insert was skipped as a duplicate (same check/minute/region); true otherwise.</returns>
    Task<bool> CreateAsync(CheckDataPoint dataPoint, CancellationToken ct = default);

    /// <summary>Returns daily avg/min/max latency across all checks of a service.</summary>
    /// <param name="serviceId">The service whose checks' data points are aggregated.</param>
    /// <param name="from">Inclusive lower bound (unix seconds).</param>
    /// <param name="to">Inclusive upper bound (unix seconds).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<(long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByServiceIdAsync(int serviceId, long from, long to, CancellationToken ct = default);

    /// <summary>Returns the most recent data point across all checks of a service.</summary>
    /// <param name="serviceId">The service whose checks' data points are searched.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CheckDataPoint?> GetLatestByServiceIdAsync(int serviceId, CancellationToken ct = default);

    /// <summary>Returns daily avg/min/max latency for a check, grouped by worker region.</summary>
    /// <param name="checkId">The check to aggregate data points for.</param>
    /// <param name="from">Inclusive lower bound (unix seconds).</param>
    /// <param name="to">Inclusive upper bound (unix seconds).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<(string Region, long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByRegionAsync(
        int checkId, long from, long to, CancellationToken ct = default);

    /// <summary>Returns up/down/degraded counts and avg latency grouped by region and day.</summary>
    /// <param name="checkId">The check to aggregate data points for.</param>
    /// <param name="from">Inclusive lower bound (unix seconds).</param>
    /// <param name="to">Inclusive upper bound (unix seconds).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<CheckDailyStats>> GetDailyStatsByCheckIdAsync(
        int checkId, long from, long to, CancellationToken ct = default);
}

public record CheckDailyStats(
    string Region,
    long DayTimestamp,
    int CountUp,
    int CountDown,
    int CountDegraded,
    double? AvgLatencyMs);
