namespace Piro.Domain.Enums;

/// <summary>
/// The stable wire names of the notification event catalog (RFC 0009 §4.2), as compile-time constants
/// so they can be used in <c>case</c> labels, <c>[NotificationEvent(...)]</c> attributes, and payload
/// <c>EventType</c> members without repeating the literal string. This is the single source of the
/// names; <see cref="NotificationEventType"/> annotates each enum value with the matching constant, and
/// payloads/handlers reference these instead of hardcoding <c>"incident:created"</c> everywhere.
/// </summary>
public static class NotificationEventNames
{
    public const string AlertCreated = "alert:created";
    public const string AlertAcknowledged = "alert:acknowledged";
    public const string AlertResolved = "alert:resolved";

    public const string IncidentCreated = "incident:created";
    public const string IncidentResolved = "incident:resolved";

    public const string SystemIntegrationExpired = "system:integration:expired";
}
