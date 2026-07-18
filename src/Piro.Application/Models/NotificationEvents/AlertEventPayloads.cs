using Piro.Domain.Enums;

namespace Piro.Application.Models.NotificationEvents;

/// <summary>
/// Payload for <c>alert:created</c> (RFC 0009 §4.2, §4.3). A flat snapshot of the alert as it was
/// opened. Additive-only: add optional fields, never rename/retype/remove an existing one.
/// </summary>
public record AlertCreatedPayloadV1(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    /// <summary>The service's effective tags at emit time — enables tag-based subscription filters (RFC 0008).</summary>
    IReadOnlyList<string> Tags,
    /// <summary>True for a third-party alert with no correlated Check/Service (RFC 0001).</summary>
    bool IsExternal,
    /// <summary>Origin label for an external alert (e.g. "GCP Cloud Monitoring"), else null.</summary>
    string? SourceLabel,
    DateTimeOffset FiredAt
) : INotificationEvent
{
    public string EventType => "alert:created";
    public int Version => 1;
}

/// <summary>Payload for <c>alert:acknowledged</c> — a human took ownership of an active alert.</summary>
public record AlertAcknowledgedPayloadV1(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    IReadOnlyList<string> Tags,
    /// <summary>Display name of the acknowledging user, if known.</summary>
    string? AcknowledgedBy,
    DateTimeOffset AcknowledgedAt
) : INotificationEvent
{
    public string EventType => "alert:acknowledged";
    public int Version => 1;
}

/// <summary>Payload for <c>alert:resolved</c> — the alert cleared (recovered or was resolved).</summary>
public record AlertResolvedPayloadV1(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    IReadOnlyList<string> Tags,
    DateTimeOffset ResolvedAt
) : INotificationEvent
{
    public string EventType => "alert:resolved";
    public int Version => 1;
}
