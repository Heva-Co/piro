using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="CheckDataPoint"/> time-series records.</summary>
public interface ICheckDataPointRepository
{
    Task<IEnumerable<CheckDataPoint>> GetByCheckIdAsync(int checkId, long? from = null, long? to = null, CancellationToken ct = default);
    Task CreateAsync(CheckDataPoint dataPoint, CancellationToken ct = default);
    Task<IEnumerable<(long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByServiceIdAsync(int serviceId, long from, long to, CancellationToken ct = default);
    Task<CheckDataPoint?> GetLatestByServiceIdAsync(int serviceId, CancellationToken ct = default);

    /// <summary>
    /// Returns average latency grouped by region and day for a multi-region check.
    /// Used to render per-region latency breakdowns on the check detail page.
    /// </summary>
    Task<IEnumerable<(string Region, long DayTimestamp, double Avg, double Min, double Max)>> GetDailyLatencyByRegionAsync(
        int checkId, long from, long to, CancellationToken ct = default);
}
