namespace Piro.Domain.Enums;

/// <summary>
/// Draft/publish lifecycle of a postmortem report. Mirrors the incident publish-lifecycle
/// precedent (<see cref="IncidentVisibility"/>). "Published" is internal-facing in Phase 1 —
/// public status-page visibility is a later phase (RFC 0005 §6).
/// </summary>
public enum PostmortemStatus
{
    Draft,
    Published
}
