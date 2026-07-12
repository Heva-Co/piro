using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>
/// Read-only aggregation queries spanning Incident/Alert/Service for the dashboard metrics view.
/// Kept separate from <see cref="IIncidentRepository"/>/<see cref="IAlertRepository"/> since these
/// are cross-entity analytical queries, not part of either aggregate's own persistence contract.
/// </summary>
public interface IMetricsRepository
{
    Task<DashboardMetricsDto> GetDashboardMetricsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
