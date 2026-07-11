using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IIncidentRepository"/>.</summary>
public class IncidentRepository(PiroDbContext db) : IIncidentRepository
{
    public async Task<IEnumerable<Incident>> GetAllAsync(string filter = "active", CancellationToken ct = default)
    {
        var query = IncidentBaseQuery();

        query = filter.ToLowerInvariant() switch
        {
            "all"      => query,
            "resolved" => query.Where(i => i.Status == IncidentStatus.Resolved),
            "active"   => query.Where(i => i.Status != IncidentStatus.Resolved),
            var state  => Enum.TryParse<IncidentStatus>(state, ignoreCase: true, out var parsed)
                            ? query.Where(i => i.Status == parsed)
                            : query.Where(i => i.Status != IncidentStatus.Resolved),
        };

        return await query.OrderByDescending(i => i.StartDateTime).ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetAllPublicAsync(bool includeResolved = false, CancellationToken ct = default)
    {
        var query = IncidentBaseQuery()
            .Where(i => i.Visibility == IncidentVisibility.Public)
            .Where(i => !db.IncidentMerges.Any(m => m.SourceIncidentId == i.Id));

        if (!includeResolved)
            query = query.Where(i => i.Status != IncidentStatus.Resolved);

        return await query.OrderByDescending(i => i.StartDateTime).ToListAsync(ct);
    }

    private IQueryable<Incident> IncidentBaseQuery() =>
        db.Incidents
            .Include(i => i.TimelineEvents.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.TriggeringCheck)
            .Include(i => i.MergesAsSource)
            .Include(i => i.ImpactChanges.OrderBy(c => c.Timestamp))
            .AsQueryable();

    public async Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.TimelineEvents.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.TriggeringCheck)
            .Include(i => i.MergesAsSource)
            .Include(i => i.ImpactChanges.OrderBy(c => c.Timestamp))
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    /// <summary>Returns the incident only if it is publicly visible — used for anonymous/public access.</summary>
    public async Task<Incident?> GetPublicByIdAsync(int id, CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.TimelineEvents.Where(e => e.Visibility == EventVisibility.Public).OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.TriggeringCheck)
            .Include(i => i.MergesAsSource)
            .Include(i => i.ImpactChanges.OrderBy(c => c.Timestamp))
            .FirstOrDefaultAsync(i => i.Id == id && i.Visibility == IncidentVisibility.Public, ct);

    public async Task<IncidentVisibility?> GetVisibilityAsync(int id, CancellationToken ct = default) =>
        await db.Incidents
            .Where(i => i.Id == id)
            .Select(i => (IncidentVisibility?)i.Visibility)
            .FirstOrDefaultAsync(ct);

    public async Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default)
    {
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task<Incident> UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        // The row may have been deleted (or resolved-and-merged) by another request between when
        // this incident was loaded and now — e.g. a background job holding a stale copy across a
        // multi-second dispatch loop while someone deletes/merges it concurrently. Check first
        // rather than catching DbUpdateConcurrencyException: no point writing if it's already gone.
        if (!await db.Incidents.AnyAsync(i => i.Id == incident.Id, ct))
            return incident;

        // `incident` is already tracked by `db` (loaded via GetByIdAsync/GetOpenWithEscalationAsync
        // in this same scope) — no explicit Update() call, so SaveChanges only persists the
        // properties actually modified by the caller instead of overwriting the whole row and
        // clobbering concurrent changes (e.g. a user ACK racing the escalation job).
        await db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task AddTimelineEventAsync(IncidentTimelineEvent evt, CancellationToken ct = default)
    {
        db.IncidentTimelineEvents.Add(evt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IncidentTimelineEvent?> GetTimelineEventByIdAsync(int incidentId, int eventId, CancellationToken ct = default) =>
        await db.IncidentTimelineEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.IncidentId == incidentId, ct);

    public async Task<int> UpdateTimelineEventAsync(
        int incidentId, int eventId, string text, EventVisibility requestedVisibility, CancellationToken ct = default)
    {
        // Single atomic statement: the requested visibility is only honored if the parent
        // incident is Public *at the moment this UPDATE executes*, closing the TOCTOU window
        // that existed when visibility was read separately before the write.
        return await db.IncidentTimelineEvents
            .Where(e => e.Id == eventId && e.IncidentId == incidentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Comment, text)
                .SetProperty(e => e.Visibility, e =>
                    requestedVisibility == EventVisibility.Public
                    && db.Incidents.Any(i => i.Id == incidentId && i.Visibility == IncidentVisibility.Public)
                        ? EventVisibility.Public
                        : EventVisibility.Private),
                ct);
    }

    public async Task DeleteTimelineEventAsync(IncidentTimelineEvent evt, CancellationToken ct = default)
    {
        db.IncidentTimelineEvents.Remove(evt);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddServiceAsync(Incident incident, IncidentService service, CancellationToken ct = default)
    {
        db.IncidentServices.Add(service);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateServiceImpactAsync(IncidentService service, CancellationToken ct = default)
    {
        db.IncidentServices.Update(service);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveServiceAsync(IncidentService service, CancellationToken ct = default)
    {
        db.IncidentServices.Remove(service);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Incident incident, CancellationToken ct = default)
    {
        db.Incidents.Remove(incident);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceStatus?> GetActiveImpactForServiceAsync(int serviceId, CancellationToken ct = default)
    {
        // Direct link impact — only Public incidents may affect the publicly-shown status
        var directImpacts = await db.IncidentServices
            .Where(s => s.ServiceId == serviceId
                     && s.Incident.Status != IncidentStatus.Resolved
                     && s.Incident.Visibility == IncidentVisibility.Public)
            .Select(s => s.Impact)
            .ToListAsync(ct);

        // Global incidents (affect every service) — use DEGRADED as minimum impact
        var hasGlobal = await db.Incidents
            .AnyAsync(i => i.Status != IncidentStatus.Resolved && i.IsGlobal && i.Visibility == IncidentVisibility.Public, ct);

        if (!directImpacts.Any() && !hasGlobal) return null;

        var worst = ServiceStatus.NO_DATA;
        foreach (var impact in directImpacts)
            worst = Worst(worst, impact);

        if (hasGlobal)
            worst = Worst(worst, ServiceStatus.DEGRADED);

        return worst;
    }

    public async Task<Incident?> GetOpenAlertIncidentForServiceAsync(int serviceId, CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.IncidentServices)
            .FirstOrDefaultAsync(i =>
                i.Source == "ALERT" &&
                i.Status != IncidentStatus.Resolved &&
                !i.IsGlobal &&
                i.IncidentServices.Any(s => s.ServiceId == serviceId), ct);

    public async Task<List<Incident>> GetRecentAlertIncidentsAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        var sinceUnix = since.ToUnixTimeSeconds();
        return await db.Incidents
            .Include(i => i.IncidentServices)
            .Where(i =>
                i.Source == "ALERT" &&
                i.Status != IncidentStatus.Resolved &&
                !i.IsGlobal &&
                i.StartDateTime >= sinceUnix)
            .ToListAsync(ct);
    }

    public async Task<Incident?> GetOpenGlobalAlertIncidentAsync(CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.IncidentServices)
            .FirstOrDefaultAsync(i =>
                i.Source == "ALERT" &&
                i.Status != IncidentStatus.Resolved &&
                i.IsGlobal, ct);

    public async Task PublishAsync(int incidentId, CancellationToken ct = default)
    {
        await db.Incidents
            .Where(i => i.Id == incidentId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Visibility, IncidentVisibility.Public), ct);
    }

    public async Task UnpublishAsync(int incidentId, CancellationToken ct = default)
    {
        await db.Incidents
            .Where(i => i.Id == incidentId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Visibility, IncidentVisibility.Private), ct);
    }

    public async Task MakeAllTimelineEventsPrivateAsync(int incidentId, CancellationToken ct = default)
    {
        await db.IncidentTimelineEvents
            .Where(e => e.IncidentId == incidentId && e.Visibility == EventVisibility.Public)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Visibility, EventVisibility.Private), ct);
    }

    public async Task AddMergeAsync(IncidentMerge merge, CancellationToken ct = default)
    {
        db.IncidentMerges.Add(merge);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddImpactChangeAsync(Incident incident, IncidentImpactChange change, CancellationToken ct = default)
    {
        incident.CurrentImpact = change.Impact;
        db.IncidentImpactChanges.Add(change);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Incident>> GetOpenWithEscalationAsync(CancellationToken ct = default) =>
        await db.Incidents
            .Where(i => i.Status != Piro.Domain.Enums.IncidentStatus.Resolved && i.EscalationPolicyId != null)
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

    private static ServiceStatus Worst(ServiceStatus a, ServiceStatus b) =>
        (int)a > (int)b ? a : b;
}
