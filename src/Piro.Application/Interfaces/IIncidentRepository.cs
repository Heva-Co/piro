using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Incident"/> aggregate (comments and service links included).</summary>
public interface IIncidentRepository
{
    /// <summary>
    /// Returns incidents filtered by <paramref name="filter"/>:
    /// "active" (default) = non-resolved, "all" = everything, "resolved" = only resolved,
    /// or an <see cref="IncidentState"/> name (e.g. "investigating").
    /// </summary>
    Task<IEnumerable<Incident>> GetAllAsync(string filter = "active", CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default);
    Task<Incident> UpdateAsync(Incident incident, CancellationToken ct = default);
    Task AddCommentAsync(Incident incident, IncidentComment comment, CancellationToken ct = default);
    Task<IncidentComment?> GetCommentByIdAsync(int incidentId, int commentId, CancellationToken ct = default);
    Task UpdateCommentAsync(IncidentComment comment, CancellationToken ct = default);
    Task DeleteCommentAsync(IncidentComment comment, CancellationToken ct = default);
    Task AddServiceAsync(Incident incident, IncidentService service, CancellationToken ct = default);
    Task UpdateServiceImpactAsync(IncidentService service, CancellationToken ct = default);
    Task RemoveServiceAsync(IncidentService service, CancellationToken ct = default);
    Task DeleteAsync(Incident incident, CancellationToken ct = default);

    /// <summary>
    /// Returns the worst impact status from all active incidents that affect
    /// <paramref name="serviceId"/> — either via a direct link or via a global incident.
    /// Returns <c>null</c> when no active incident affects the service.
    /// </summary>
    Task<ServiceStatus?> GetActiveImpactForServiceAsync(int serviceId, CancellationToken ct = default);

    /// <summary>
    /// Returns the open ALERT-sourced incident for a service, if one exists.
    /// Used by auto-create logic to attach new checks to an existing per-service incident.
    /// </summary>
    Task<Incident?> GetOpenAlertIncidentForServiceAsync(int serviceId, CancellationToken ct = default);

    /// <summary>
    /// Returns all open ALERT-sourced per-service incidents created within the given window.
    /// Used by Hybrid/Global correlation to count and merge simultaneous failures.
    /// </summary>
    Task<List<Incident>> GetRecentAlertIncidentsAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Returns the open global incident if one exists.</summary>
    Task<Incident?> GetOpenGlobalAlertIncidentAsync(CancellationToken ct = default);

    /// <summary>Publishes an incident by setting <see cref="Incident.IsPublic"/> to true.</summary>
    Task PublishAsync(Incident incident, CancellationToken ct = default);

    /// <summary>Records a merge between a per-service incident and a global incident.</summary>
    Task AddMergeAsync(IncidentMerge merge, CancellationToken ct = default);

    /// <summary>Returns all checks that are currently alerting on the given service.</summary>
    Task<int> CountAlertingChecksOnServiceAsync(int serviceId, CancellationToken ct = default);
}
