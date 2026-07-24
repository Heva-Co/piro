using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Application.Services;

/// <summary>Aggregated incident-response metrics (MTTA, MTTR, MTTD, alert noise) for the dashboard.</summary>
public class DashboardAppService(IMetricsRepository metricsRepository)
{
    /// <summary>
    /// Defaults to month-to-date (UTC) when <paramref name="from"/>/<paramref name="to"/> are omitted:
    /// from the 1st of the current month through today inclusive — not the whole calendar month. The
    /// range is half-open [from, to), so <c>to</c> is tomorrow. This keeps the daily-volume chart from
    /// showing empty future days and matches the "Month to Date" label on the dashboard.
    /// </summary>
    public Task<DashboardMetricsDto> GetMetricsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo = to ?? today.AddDays(1);

        return metricsRepository.GetDashboardMetricsAsync(
            new DateTimeOffset(rangeFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(rangeTo.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            ct);
    }
}
