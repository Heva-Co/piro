using Piro.Domain.Enums;

namespace Piro.Application.Models;

/// <summary>
/// Content for an incident notification (RFC 0009 §4.2) — a distinct content type from
/// <see cref="AlertNotificationContext"/>, since an incident is not an alert (no check/service/severity;
/// it has a title, a status, affected services, and a visibility). A dispatcher that can carry incident
/// notifications renders this shape; keeping it a separate type is what makes "this channel renders
/// incidents this way" a per-content decision on the dispatcher, not a coupling on the content.
/// </summary>
public record IncidentNotificationContext(
    int IncidentId,
    string Title,
    IncidentStatus Status,
    /// <summary>True once the incident reached a final state (Resolved/Merged) — the "resolved" notification.</summary>
    bool IsResolved,
    IncidentVisibility Visibility,
    /// <summary>Names of the services this incident affects.</summary>
    IReadOnlyList<string> AffectedServices,
    DateTimeOffset OccurredAt
) : INotificationContent;
