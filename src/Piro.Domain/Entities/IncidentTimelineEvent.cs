using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A single lifecycle event recorded on an <see cref="Incident"/> — creation, status change,
/// manual comment, acknowledgement, service add/remove, merge, or publish/unpublish.
/// Uses shared nullable columns rather than JSON so each event type keeps only the fields
/// that apply to it, while the whole timeline stays queryable/orderable with plain SQL/LINQ.
/// </summary>
public class IncidentTimelineEvent
{
    public int Id { get; set; }
    public int IncidentId { get; set; }

    public TimelineEventType Type { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Display name of the user who triggered this event. Null for automatic/system events.</summary>
    public string? ActorName { get; set; }

    /// <summary>Comment text. Only set for <see cref="TimelineEventType.CommentPosted"/>.</summary>
    public string? Comment { get; set; }

    /// <summary>Status before the change. Only set for <see cref="TimelineEventType.StatusChanged"/>.</summary>
    public IncidentStatus? OldStatus { get; set; }

    /// <summary>
    /// Status after the change. Set for <see cref="TimelineEventType.StatusChanged"/>, and also for
    /// <see cref="TimelineEventType.CommentPosted"/> when the post advances the incident's status.
    /// </summary>
    public IncidentStatus? NewStatus { get; set; }

    /// <summary>
    /// Controls visibility on the public status page. An event can only be Public if the parent
    /// incident is also Public, and events default to Private regardless of the incident's visibility —
    /// only <see cref="TimelineEventType.CommentPosted"/> can be explicitly made Public.
    /// </summary>
    public EventVisibility Visibility { get; set; } = EventVisibility.Private;

    /// <summary>The other incident involved in the merge. Only set for <see cref="TimelineEventType.MergedTo"/>/<see cref="TimelineEventType.MergedFrom"/>.</summary>
    public int? RelatedIncidentId { get; set; }

    public Incident Incident { get; set; } = null!;
}
