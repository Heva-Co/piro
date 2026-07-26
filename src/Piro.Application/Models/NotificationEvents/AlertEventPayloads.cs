using Piro.Domain.Enums;

namespace Piro.Application.Models.NotificationEvents;

/// <summary>
/// Payload for <c>alert:created</c> (RFC 0009 §4.2, §4.3). A flat snapshot of the alert as it was
/// opened. Additive-only: add optional fields, never rename/retype/remove an existing one — the one
/// deliberate exception is <see cref="Tags"/> in v3 (see its remark).
/// </summary>
public record AlertCreatedPayload(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    /// <summary>The service's effective tags (key → value, value null for a key-only tag) at emit time,
    /// enabling tag-based subscription filters (RFC 0008, #203). Retyped from a flat string list in v3 —
    /// a one-time break, safe because the field was never populated (always empty) before v3.</summary>
    IReadOnlyDictionary<string, string?> Tags,
    /// <summary>True for a third-party alert with no correlated Check/Service (RFC 0001).</summary>
    bool IsExternal,
    /// <summary>Origin label for an external alert (e.g. "GCP Cloud Monitoring"), else null.</summary>
    string? SourceLabel,
    DateTimeOffset FiredAt,
    /// <summary>Piro Service id, for resolving per-service routing. Null for an orphan/external alert. Added in v2.</summary>
    int? ServiceId = null
) : INotificationEvent
{
    public string EventType => NotificationEventNames.AlertCreated;
    public int Version => 3;
}

/// <summary>Payload for <c>alert:acknowledged</c> — a human took ownership of an active alert.</summary>
public record AlertAcknowledgedPayload(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    /// <summary>Service effective tags (key → value) at emit time. Retyped from a string list in v3.</summary>
    IReadOnlyDictionary<string, string?> Tags,
    /// <summary>Display name of the acknowledging user, if known.</summary>
    string? AcknowledgedBy,
    DateTimeOffset AcknowledgedAt,
    /// <summary>Piro Service id, for per-service routing. Null for an orphan/external alert. Added in v2.</summary>
    int? ServiceId = null
) : INotificationEvent
{
    public string EventType => NotificationEventNames.AlertAcknowledged;
    public int Version => 3;
}

/// <summary>Payload for <c>alert:resolved</c> — the alert cleared (recovered or was resolved).</summary>
public record AlertResolvedPayload(
    int AlertId,
    string ServiceName,
    string CheckName,
    AlertSeverity Severity,
    /// <summary>Service effective tags (key → value) at emit time. Retyped from a string list in v3.</summary>
    IReadOnlyDictionary<string, string?> Tags,
    DateTimeOffset ResolvedAt,
    /// <summary>Piro Service id, for per-service routing. Null for an orphan/external alert. Added in v2.</summary>
    int? ServiceId = null
) : INotificationEvent
{
    public string EventType => NotificationEventNames.AlertResolved;
    public int Version => 3;
}
