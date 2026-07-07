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
            "resolved" => query.Where(i => i.State == IncidentState.Resolved),
            "active"   => query.Where(i => i.State != IncidentState.Resolved),
            var state  => Enum.TryParse<IncidentState>(state, ignoreCase: true, out var parsed)
                            ? query.Where(i => i.State == parsed)
                            : query.Where(i => i.State != IncidentState.Resolved),
        };

        return await query.OrderByDescending(i => i.StartDateTime).ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetAllPublicAsync(bool includeResolved = false, CancellationToken ct = default)
    {
        var query = IncidentBaseQuery()
            .Where(i => i.IsPublic)
            .Where(i => !db.IncidentMerges.Any(m => m.SourceIncidentId == i.Id));

        if (!includeResolved)
            query = query.Where(i => i.State != IncidentState.Resolved);

        return await query.OrderByDescending(i => i.StartDateTime).ToListAsync(ct);
    }

    private IQueryable<Incident> IncidentBaseQuery() =>
        db.Incidents
            .Include(i => i.Comments.OrderBy(c => c.CommentedAt))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.TriggeringCheck)
            .Include(i => i.MergesAsSource)
            .Include(i => i.ImpactChanges.OrderBy(c => c.Timestamp))
            .AsQueryable();

    public async Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.Comments.OrderBy(c => c.CommentedAt))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.TriggeringCheck)
            .Include(i => i.MergesAsSource)
            .Include(i => i.ImpactChanges.OrderBy(c => c.Timestamp))
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Incident> CreateAsync(Incident incident, CancellationToken ct = default)
    {
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task<Incident> UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        db.Incidents.Update(incident);
        await db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task AddCommentAsync(Incident incident, IncidentComment comment, CancellationToken ct = default)
    {
        db.IncidentComments.Add(comment);
        db.Incidents.Update(incident);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IncidentComment?> GetCommentByIdAsync(int incidentId, int commentId, CancellationToken ct = default) =>
        await db.IncidentComments.FirstOrDefaultAsync(c => c.Id == commentId && c.IncidentId == incidentId, ct);

    public async Task UpdateCommentAsync(IncidentComment comment, CancellationToken ct = default)
    {
        db.IncidentComments.Update(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCommentAsync(IncidentComment comment, CancellationToken ct = default)
    {
        db.IncidentComments.Remove(comment);
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
        // Direct link impact
        var directImpacts = await db.IncidentServices
            .Where(s => s.ServiceId == serviceId && s.Incident.State != IncidentState.Resolved)
            .Select(s => s.Impact)
            .ToListAsync(ct);

        // Global incidents (affect every service) — use DEGRADED as minimum impact
        var hasGlobal = await db.Incidents
            .AnyAsync(i => i.State != IncidentState.Resolved && i.IsGlobal, ct);

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
                i.State != IncidentState.Resolved &&
                !i.IsGlobal &&
                i.IncidentServices.Any(s => s.ServiceId == serviceId), ct);

    public async Task<List<Incident>> GetRecentAlertIncidentsAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        var sinceUnix = since.ToUnixTimeSeconds();
        return await db.Incidents
            .Include(i => i.IncidentServices)
            .Where(i =>
                i.Source == "ALERT" &&
                i.State != IncidentState.Resolved &&
                !i.IsGlobal &&
                i.StartDateTime >= sinceUnix)
            .ToListAsync(ct);
    }

    public async Task<Incident?> GetOpenGlobalAlertIncidentAsync(CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.IncidentServices)
            .FirstOrDefaultAsync(i =>
                i.Source == "ALERT" &&
                i.State != IncidentState.Resolved &&
                i.IsGlobal, ct);

    public async Task PublishAsync(Incident incident, CancellationToken ct = default)
    {
        incident.IsPublic = true;
        db.Incidents.Update(incident);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddMergeAsync(IncidentMerge merge, CancellationToken ct = default)
    {
        db.IncidentMerges.Add(merge);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddImpactChangeAsync(Incident incident, IncidentImpactChange change, CancellationToken ct = default)
    {
        incident.CurrentImpact = change.Impact;
        db.Incidents.Update(incident);
        db.IncidentImpactChanges.Add(change);
        await db.SaveChangesAsync(ct);
    }

    private static ServiceStatus Worst(ServiceStatus a, ServiceStatus b) =>
        (int)a > (int)b ? a : b;
}
