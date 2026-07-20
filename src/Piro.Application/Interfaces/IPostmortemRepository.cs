using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for the <see cref="Postmortem"/> aggregate (RFC 0005).</summary>
public interface IPostmortemRepository
{
    /// <summary>Returns all postmortems, newest first, with owner + linked incidents loaded for list rendering.</summary>
    Task<IEnumerable<Postmortem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a single postmortem with its field values (joined to definitions), referenced incidents,
    /// and each referenced incident's timeline events / impact changes / alerts (for the derived timeline).
    /// </summary>
    Task<Postmortem?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Postmortem> CreateAsync(Postmortem postmortem, CancellationToken ct = default);
    Task<Postmortem> UpdateAsync(Postmortem postmortem, CancellationToken ct = default);
    Task DeleteAsync(Postmortem postmortem, CancellationToken ct = default);

    /// <summary>Returns the active (IsActive) field definitions, ordered by SortOrder — the analysis template.</summary>
    Task<List<PostmortemFieldDefinition>> GetActiveFieldDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Links an incident to a postmortem. No-op (returns false) if the link already exists or the incident doesn't exist.</summary>
    Task<bool> LinkIncidentAsync(int postmortemId, int incidentId, CancellationToken ct = default);

    /// <summary>Removes an incident link. Returns false if the link didn't exist.</summary>
    Task<bool> UnlinkIncidentAsync(int postmortemId, int incidentId, CancellationToken ct = default);

    /// <summary>True if the incident exists — used to validate a link request.</summary>
    Task<bool> IncidentExistsAsync(int incidentId, CancellationToken ct = default);

    /// <summary>Adds an author annotation to the report's timeline (RFC 0005 §4.4).</summary>
    Task<PostmortemTimelineEntry> AddTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default);

    /// <summary>Loads a single annotation scoped to its parent report; null if it doesn't exist.</summary>
    Task<PostmortemTimelineEntry?> GetTimelineEntryAsync(int postmortemId, int entryId, CancellationToken ct = default);

    Task UpdateTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default);
    Task DeleteTimelineEntryAsync(PostmortemTimelineEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns incidents whose active window overlaps [<paramref name="from"/>, <paramref name="to"/>] and
    /// aren't already linked to the report — the impact-window suggestion set (RFC 0005 §4.6).
    /// </summary>
    Task<List<Incident>> GetIncidentSuggestionsAsync(int postmortemId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
