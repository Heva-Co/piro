namespace Piro.Domain.Entities;

/// <summary>
/// An author-owned annotation on a postmortem's timeline (RFC 0005 §4.4, Phase 2) — the human
/// judgement the machine couldn't record ("vendor confirmed the outage at 14:32"). These are merged
/// chronologically with the read-only events derived from the referenced incidents; only these entries
/// are add/edit/deletable, and they live on the report, never written back onto the incident.
/// </summary>
public class PostmortemTimelineEntry
{
    public int Id { get; set; }

    /// <summary>FK to the parent <see cref="Postmortem"/>, cascade delete.</summary>
    public int PostmortemId { get; set; }

    /// <summary>When the annotated event happened, so it sorts into the merged timeline.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The annotation text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Denormalized author display name, mirroring <see cref="IncidentTimelineEvent.ActorName"/>.</summary>
    public string? AuthorName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Postmortem Postmortem { get; set; } = null!;
}
