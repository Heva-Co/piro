using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IIncidentRepository"/>.</summary>
public class IncidentRepository(PiroDbContext db) : IIncidentRepository
{
    public async Task<IEnumerable<Incident>> GetAllAsync(bool includeResolved = false, CancellationToken ct = default)
    {
        var query = db.Incidents
            .Include(i => i.Comments.OrderBy(c => c.CommentedAt))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
            .AsQueryable();

        if (!includeResolved)
            query = query.Where(i => i.Status == IncidentStatus.Active);

        return await query.OrderByDescending(i => i.StartDateTime).ToListAsync(ct);
    }

    public async Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Incidents
            .Include(i => i.Comments.OrderBy(c => c.CommentedAt))
            .Include(i => i.IncidentServices)
                .ThenInclude(s => s.Service)
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
            .Where(s => s.ServiceId == serviceId && s.Incident.Status == IncidentStatus.Active)
            .Select(s => s.Impact)
            .ToListAsync(ct);

        // Global incidents (affect every service) — use DEGRADED as minimum impact
        var hasGlobal = await db.Incidents
            .AnyAsync(i => i.Status == IncidentStatus.Active && i.IsGlobal, ct);

        if (!directImpacts.Any() && !hasGlobal) return null;

        var worst = ServiceStatus.NO_DATA;
        foreach (var impact in directImpacts)
            worst = Worst(worst, impact);

        if (hasGlobal)
            worst = Worst(worst, ServiceStatus.DEGRADED);

        return worst;
    }

    private static ServiceStatus Worst(ServiceStatus a, ServiceStatus b) =>
        (int)a > (int)b ? a : b;
}
