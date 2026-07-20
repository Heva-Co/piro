using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IPostmortemRepository"/> (RFC 0005).</summary>
public class PostmortemRepository(PiroDbContext db) : IPostmortemRepository
{
    public async Task<IEnumerable<Postmortem>> GetAllAsync(CancellationToken ct = default) =>
        await db.Postmortems
            .AsSplitQuery()
            .Include(p => p.PostmortemIncidents)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<Postmortem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Postmortems
            .AsSplitQuery()
            .Include(p => p.FieldValues)
                .ThenInclude(v => v.FieldDefinition)
            .Include(p => p.PostmortemIncidents)
                .ThenInclude(pi => pi.Incident)
                    .ThenInclude(i => i.TimelineEvents)
            .Include(p => p.PostmortemIncidents)
                .ThenInclude(pi => pi.Incident)
                    .ThenInclude(i => i.ImpactChanges)
            .Include(p => p.PostmortemIncidents)
                .ThenInclude(pi => pi.Incident)
                    .ThenInclude(i => i.Alerts)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Postmortem> CreateAsync(Postmortem postmortem, CancellationToken ct = default)
    {
        db.Postmortems.Add(postmortem);
        await db.SaveChangesAsync(ct);
        return postmortem;
    }

    public async Task<Postmortem> UpdateAsync(Postmortem postmortem, CancellationToken ct = default)
    {
        // `postmortem` is already tracked (loaded via GetByIdAsync in this scope) — no explicit Update()
        // so SaveChanges persists only the modified properties.
        await db.SaveChangesAsync(ct);
        return postmortem;
    }

    public async Task DeleteAsync(Postmortem postmortem, CancellationToken ct = default)
    {
        db.Postmortems.Remove(postmortem);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PostmortemFieldDefinition>> GetActiveFieldDefinitionsAsync(CancellationToken ct = default) =>
        await db.PostmortemFieldDefinitions
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

    public async Task<bool> LinkIncidentAsync(int postmortemId, int incidentId, CancellationToken ct = default)
    {
        var exists = await db.PostmortemIncidents
            .AnyAsync(pi => pi.PostmortemId == postmortemId && pi.IncidentId == incidentId, ct);
        if (exists) return false;

        db.PostmortemIncidents.Add(new PostmortemIncident { PostmortemId = postmortemId, IncidentId = incidentId });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnlinkIncidentAsync(int postmortemId, int incidentId, CancellationToken ct = default)
    {
        var link = await db.PostmortemIncidents
            .FirstOrDefaultAsync(pi => pi.PostmortemId == postmortemId && pi.IncidentId == incidentId, ct);
        if (link is null) return false;

        db.PostmortemIncidents.Remove(link);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IncidentExistsAsync(int incidentId, CancellationToken ct = default) =>
        await db.Incidents.AnyAsync(i => i.Id == incidentId, ct);
}
