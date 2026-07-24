using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A standalone post-incident review report (RFC 0005). It has its own name, an owner of the review
/// process, an impact window, and a draft/publish lifecycle, and it <em>references</em> one or more
/// <see cref="Incident"/>s through <see cref="PostmortemIncident"/> (N:M) rather than being owned by
/// a single incident. Its factual timeline is derived at read time from the referenced incidents.
/// </summary>
public class Postmortem
{
    public int Id { get; set; }

    /// <summary>The report name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Draft (default) or Published — the review lifecycle. Internal-only in Phase 1.</summary>
    public PostmortemStatus Status { get; set; } = PostmortemStatus.Draft;

    /// <summary>
    /// FK to the <see cref="AppUser"/> who owns running the review. Nullable with ON DELETE SET NULL —
    /// deleting the user preserves the report and its <see cref="ReviewOwnerName"/> snapshot (RFC 0005 §4.7).
    /// </summary>
    public int? ReviewOwnerUserId { get; set; }

    /// <summary>
    /// Denormalized snapshot of the owner's display name at assign time, so attribution survives even
    /// after the owning <see cref="AppUser"/> is deleted and the FK nulls out (RFC 0005 §4.2, §7).
    /// </summary>
    public string? ReviewOwnerName { get; set; }

    /// <summary>Optional impact window start.</summary>
    public DateTimeOffset? ImpactStartAt { get; set; }

    /// <summary>Optional impact window end.</summary>
    public DateTimeOffset? ImpactEndAt { get; set; }

    /// <summary>Stamped when the report is published; null while Draft.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Analysis content — one value per active <see cref="PostmortemFieldDefinition"/>.</summary>
    public ICollection<PostmortemFieldValue> FieldValues { get; set; } = [];

    /// <summary>Referenced incidents, N:M (RFC 0005 §4.6).</summary>
    public ICollection<PostmortemIncident> PostmortemIncidents { get; set; } = [];

    /// <summary>Author-owned timeline annotations, merged with the derived incident events (RFC 0005 §4.4, Phase 2).</summary>
    public ICollection<PostmortemTimelineEntry> TimelineEntries { get; set; } = [];

    /// <summary>Nav to the owner. May be null after the owner is deleted (see <see cref="ReviewOwnerName"/>).</summary>
    public AppUser? ReviewOwner { get; set; }
}
