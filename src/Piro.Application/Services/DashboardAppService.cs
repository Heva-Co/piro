using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Application.Services;

/// <summary>Aggregated incident-response metrics (MTTA, MTTR, MTTD, alert noise) for the dashboard.</summary>
public class DashboardAppService(IMetricsRepository metricsRepository)
{
    /// <summary>Defaults to the current calendar month (UTC) when <paramref name="from"/>/<paramref name="to"/> are omitted.</summary>
    public Task<DashboardMetricsDto> GetMetricsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo = to ?? rangeFrom.AddMonths(1);

        return metricsRepository.GetDashboardMetricsAsync(
            new DateTimeOffset(rangeFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(rangeTo.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            ct);
    }
}
