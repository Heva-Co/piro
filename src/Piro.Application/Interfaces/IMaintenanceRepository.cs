using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Maintenance"/> definitions and their materialized events.</summary>
public interface IMaintenanceRepository
{
    Task<IEnumerable<Maintenance>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Same as <see cref="GetAllAsync"/> but loads every event regardless of status — the
    /// public status page needs the real current/most-recent event even if already Completed.</summary>
    Task<IEnumerable<Maintenance>> GetAllForPublicAsync(CancellationToken ct = default);
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

    /// <summary>Returns the IDs of all services affected by the given maintenance (all services if it is global).</summary>
    Task<IReadOnlyList<int>> GetAffectedServiceIdsAsync(int maintenanceId, CancellationToken ct = default);

    /// <summary>Returns a single materialized event by ID, or null if it doesn't exist or doesn't belong to that maintenance.</summary>
    Task<MaintenanceEvent?> GetEventByIdAsync(int maintenanceId, int eventId, CancellationToken ct = default);

    /// <summary>Marks a single event as cancelled without affecting the parent maintenance or its other events.</summary>
    Task CancelEventAsync(MaintenanceEvent maintenanceEvent, CancellationToken ct = default);
}
