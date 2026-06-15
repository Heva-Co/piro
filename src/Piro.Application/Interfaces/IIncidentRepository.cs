using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Incident"/> aggregate (comments and service links included).</summary>
public interface IIncidentRepository
{
    Task<IEnumerable<Incident>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default);
    Task<Incident> UpdateAsync(Incident incident, CancellationToken ct = default);
    Task AddCommentAsync(Incident incident, IncidentComment comment, CancellationToken ct = default);
    Task<IncidentComment?> GetCommentByIdAsync(int incidentId, int commentId, CancellationToken ct = default);
    Task UpdateCommentAsync(IncidentComment comment, CancellationToken ct = default);
    Task DeleteCommentAsync(IncidentComment comment, CancellationToken ct = default);
    Task AddServiceAsync(Incident incident, IncidentService service, CancellationToken ct = default);
    Task RemoveServiceAsync(IncidentService service, CancellationToken ct = default);
    Task DeleteAsync(Incident incident, CancellationToken ct = default);

    /// <summary>
    /// Returns the worst impact status from all active incidents that affect
    /// <paramref name="serviceId"/> — either via a direct link or via a global incident.
    /// Returns <c>null</c> when no active incident affects the service.
    /// </summary>
    Task<ServiceStatus?> GetActiveImpactForServiceAsync(int serviceId, CancellationToken ct = default);
}
