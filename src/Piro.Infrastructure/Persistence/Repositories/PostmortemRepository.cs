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
            .Include(p => p.TimelineEntries)
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

    public async Task<List<PostmortemFieldDefinition>> GetAllFieldDefinitionsAsync(CancellationToken ct = default) =>
        await db.PostmortemFieldDefinitions
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

    public async Task<PostmortemFieldDefinition?> GetFieldDefinitionAsync(int id, CancellationToken ct = default) =>
        await db.PostmortemFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<bool> FieldDefinitionKeyExistsAsync(string key, int? excludingId = null, CancellationToken ct = default) =>
        await db.PostmortemFieldDefinitions
            .AnyAsync(d => d.Key == key && (excludingId == null || d.Id != excludingId), ct);

    public async Task<int> GetMaxFieldDefinitionSortOrderAsync(CancellationToken ct = default) =>
        await db.PostmortemFieldDefinitions.AnyAsync(ct)
            ? await db.PostmortemFieldDefinitions.MaxAsync(d => d.SortOrder, ct)
            : -1;

    public async Task<PostmortemFieldDefinition> CreateFieldDefinitionAsync(PostmortemFieldDefinition definition, CancellationToken ct = default)
    {
        db.PostmortemFieldDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task UpdateFieldDefinitionAsync(PostmortemFieldDefinition definition, CancellationToken ct = default)
    {
        // `definition` is already tracked (loaded via GetFieldDefinitionAsync in this scope).
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFieldDefinitionAsync(PostmortemFieldDefinition definition, CancellationToken ct = default)
    {
        db.PostmortemFieldDefinitions.Remove(definition);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> FieldDefinitionHasValuesAsync(int definitionId, CancellationToken ct = default) =>
        await db.PostmortemFieldValues.AnyAsync(v => v.FieldDefinitionId == definitionId, ct);

    public async Task SaveFieldDefinitionOrderAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);

    public async Task<bool> BackfillFieldValuesAsync(int postmortemId, IEnumerable<int> activeDefinitionIds, CancellationToken ct = default)
    {
        var existing = await db.PostmortemFieldValues
            .Where(v => v.PostmortemId == postmortemId)
            .Select(v => v.FieldDefinitionId)
            .ToListAsync(ct);

        var missing = activeDefinitionIds.Except(existing).ToList();
        if (missing.Count == 0) return false;

        foreach (var defId in missing)
            db.PostmortemFieldValues.Add(new PostmortemFieldValue
            {
                PostmortemId = postmortemId,
                FieldDefinitionId = defId,
                Value = "",
            });
        await db.SaveChangesAsync(ct);
        return true;
    }

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

    public async Task<PostmortemTimelineEntry> AddTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default)
    {
        db.PostmortemTimelineEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<PostmortemTimelineEntry?> GetTimelineEntryAsync(int postmortemId, int entryId, CancellationToken ct = default) =>
        await db.PostmortemTimelineEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.PostmortemId == postmortemId, ct);

    public async Task UpdateTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default)
    {
        // `entry` is already tracked (loaded via GetTimelineEntryAsync in this scope).
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default)
    {
        db.PostmortemTimelineEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Incident>> GetIncidentSuggestionsAsync(
        int postmortemId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var fromUnix = from.ToUnixTimeSeconds();
        var toUnix = to.ToUnixTimeSeconds();

        var linkedIds = await db.PostmortemIncidents
            .Where(pi => pi.PostmortemId == postmortemId)
            .Select(pi => pi.IncidentId)
            .ToListAsync(ct);

        // An incident overlaps the window when it started before the window ends AND either is still
        // open (no EndDateTime) or ended after the window starts.
        return await db.Incidents
            .Where(i => !linkedIds.Contains(i.Id))
            .Where(i => i.StartDateTime <= toUnix && (i.EndDateTime == null || i.EndDateTime >= fromUnix))
            .OrderByDescending(i => i.StartDateTime)
            .ToListAsync(ct);
    }
}
