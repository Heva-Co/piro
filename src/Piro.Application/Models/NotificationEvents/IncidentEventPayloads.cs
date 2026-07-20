using Piro.Domain.Enums;

namespace Piro.Application.Models.NotificationEvents;

/// <summary>
/// Payload for <c>incident:created</c> (RFC 0009 §4.2). In v1 the payload is the same regardless of
/// the incident's visibility — subscribing to <c>incident:*</c> receives all incidents, private
/// included; that is the admin's explicit, warned-about choice (RFC 0009 §4.3). Additive-only.
/// </summary>
public record IncidentCreatedPayload(
    int IncidentId,
    string Title,
    IncidentStatus Status,
    IncidentVisibility Visibility,
    /// <summary>Names of the services this incident affects.</summary>
    IReadOnlyList<string> AffectedServices,
    DateTimeOffset CreatedAt
) : INotificationEvent
{
    public string EventType => NotificationEventNames.IncidentCreated;
    public int Version => 1;
}

/// <summary>
/// Payload for <c>incident:resolved</c> — the incident reached a final state (Resolved or Merged).
/// Same visibility caveat as <see cref="IncidentCreatedPayload"/>.
/// </summary>
public record IncidentResolvedPayload(
    int IncidentId,
    string Title,
    /// <summary>The final status: Resolved, or Merged when absorbed into another incident.</summary>
    IncidentStatus Status,
    IncidentVisibility Visibility,
    IReadOnlyList<string> AffectedServices,
    DateTimeOffset ResolvedAt
) : INotificationEvent
{
    public string EventType => NotificationEventNames.IncidentResolved;
    public int Version => 1;
}
