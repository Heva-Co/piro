using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Incident"/> aggregate (comments and service links included).</summary>
public interface IIncidentRepository
{
    /// <summary>
    /// Returns incidents filtered by <paramref name="filter"/>:
    /// "active" (default) = non-resolved, "all" = everything, "resolved" = only resolved,
    /// or an <see cref="IncidentStatus"/> name (e.g. "investigating").
    /// Admin-facing — returns all incidents regardless of <see cref="Incident.IsPublic"/>.
    /// </summary>
    Task<IEnumerable<Incident>> GetAllAsync(string filter = "active", CancellationToken ct = default);

    /// <summary>
    /// Returns only published (<see cref="Incident.IsPublic"/> = true), non-merged incidents
    /// for the public status page. Pass <paramref name="includeResolved"/> = true to include
    /// resolved incidents in the history view.
    /// </summary>
    Task<IEnumerable<Incident>> GetAllPublicAsync(bool includeResolved = false, CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns the incident only if it is publicly visible, with only public comments included. Used for anonymous/public access.</summary>
    Task<Incident?> GetPublicByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns just the incident's visibility, without loading the rest of the aggregate. Null if the incident doesn't exist.</summary>
    Task<IncidentVisibility?> GetVisibilityAsync(int id, CancellationToken ct = default);
    Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default);
    Task<Incident> UpdateAsync(Incident incident, CancellationToken ct = default);

    /// <summary>Records a new timeline event for the incident (created, status change, comment, ack, merge, service add/remove, publish/unpublish).</summary>
    Task AddTimelineEventAsync(IncidentTimelineEvent evt, CancellationToken ct = default);

    Task<IncidentTimelineEvent?> GetTimelineEventByIdAsync(int incidentId, int eventId, CancellationToken ct = default);

    /// <summary>
    /// Updates a <see cref="TimelineEventType.CommentPosted"/> event's text/status/visibility in a single
    /// atomic statement. Public visibility is only honored if the parent incident is Public at the moment
    /// of the update — this closes the race window where a concurrent Unpublish could otherwise leave a
    /// Public comment on a Private incident. Returns the number of rows affected (0 if the event doesn't exist).
    /// </summary>
    Task<int> UpdateTimelineEventAsync(int incidentId, int eventId, string text, EventVisibility requestedVisibility, CancellationToken ct = default);

    Task DeleteTimelineEventAsync(IncidentTimelineEvent evt, CancellationToken ct = default);

    /// <summary>Sets every Public timeline event on the incident back to Private in a single statement.</summary>
    Task MakeAllTimelineEventsPrivateAsync(int incidentId, CancellationToken ct = default);

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
    /// Returns all open ALERT-sourced incidents created within the given window.
    /// Used by Merge correlation to count and merge simultaneous failures.
    /// </summary>
    Task<List<Incident>> GetRecentAlertIncidentsAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>
    /// Returns an already-open merge-target incident (Source=ALERT, more than one linked service)
    /// created within the given window, if one exists. Used so a newly-correlated failure attaches
    /// to the existing merge incident instead of spawning a duplicate one.
    /// </summary>
    Task<Incident?> GetOpenMergeIncidentAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Publishes an incident by setting its <see cref="Incident.Visibility"/> to Public.</summary>
    Task PublishAsync(int incidentId, CancellationToken ct = default);

    /// <summary>Reverts an incident to Private, hiding it from the public status page again.</summary>
    Task UnpublishAsync(int incidentId, CancellationToken ct = default);

    /// <summary>
    /// Records a new impact change for the incident and updates <see cref="Incident.CurrentImpact"/>.
    /// Should only be called when the impact actually changes (caller must check).
    /// </summary>
    Task AddImpactChangeAsync(Incident incident, IncidentImpactChange change, CancellationToken ct = default);

    /// <summary>Records a merge between a per-service incident and a global incident.</summary>
    Task AddMergeAsync(IncidentMerge merge, CancellationToken ct = default);

    /// <summary>Returns all open (non-resolved) incidents that have an escalation policy assigned.</summary>
    Task<List<Incident>> GetOpenWithEscalationAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated page of an incident's timeline events, most recent first, plus the total
    /// count. Pass <paramref name="publicOnly"/> = true to restrict to <see cref="EventVisibility.Public"/>
    /// events only (anonymous/public access) — mirrors the filtering already applied by <see cref="GetPublicByIdAsync"/>.
    /// </summary>
    Task<(IEnumerable<IncidentTimelineEvent> Items, int TotalCount)> GetTimelinePagedAsync(
        int incidentId, int page, int pageSize, bool publicOnly, CancellationToken ct = default);
}
