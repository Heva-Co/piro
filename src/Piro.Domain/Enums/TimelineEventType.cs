namespace Piro.Domain.Enums;

/// <summary>Kind of lifecycle event recorded in an incident's timeline.</summary>
public enum TimelineEventType
{
    Created,
    StatusChanged,
    CommentPosted,
    Acknowledged,
    ServiceAdded,
    ServiceRemoved,
    MergedTo,
    MergedFrom,
    Published,
    Unpublished,
    AlertFired
}
