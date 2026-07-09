using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Maintenance"/> definitions and their materialized events.</summary>
public interface IMaintenanceRepository
{
    Task<IEnumerable<Maintenance>> GetAllAsync(CancellationToken ct = default);
    Task<Maintenance?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Maintenance>> GetActiveAsync(CancellationToken ct = default);
    Task<Maintenance> CreateAsync(Maintenance maintenance, CancellationToken ct = default);
    Task<Maintenance> UpdateAsync(Maintenance maintenance, CancellationToken ct = default);
    Task DeleteAsync(Maintenance maintenance, CancellationToken ct = default);
    Task AddEventsAsync(IEnumerable<MaintenanceEvent> events, CancellationToken ct = default);
    Task DeleteFutureEventsAsync(int maintenanceId, long fromTimestamp, CancellationToken ct = default);

    /// <summary>Returns ongoing or upcoming events (for status computation and scheduler).</summary>
    Task<IEnumerable<MaintenanceEvent>> GetActiveEventsAsync(CancellationToken ct = default);

    /// <summary>Returns true if <paramref name="serviceId"/> is affected by a currently ongoing maintenance event.</summary>
    Task<bool> HasActiveWindowAsync(int serviceId, CancellationToken ct = default);
}
