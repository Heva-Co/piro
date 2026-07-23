namespace Piro.Contracts;

/// <summary>
/// Fields common to every alert-family event (RFC 0009 <c>alert:*</c>). Not an event itself — the
/// concrete alert events derive from <see cref="Event"/> and carry these via composition-by-record.
/// </summary>
public abstract record AlertEvent : Event
{
    /// <summary>Name of the service that owns the check (or a display placeholder for an external alert).</summary>
    public required string ServiceName { get; init; }

    /// <summary>Name of the check that triggered the alert (or a placeholder for an external alert).</summary>
    public required string CheckName { get; init; }

    /// <summary>Current status that caused the alert, as a neutral display string (e.g. "Down", "Degraded").</summary>
    public string? CurrentStatus { get; init; }

    /// <summary>Human-readable description of the alert (optional).</summary>
    public string? Description { get; init; }

    public int AlertId { get; init; }
    public int CheckId { get; init; }
    public string? Value { get; init; }
    public int FailureThreshold { get; init; } = 1;
    public int SuccessThreshold { get; init; } = 1;

    public string? ServiceUrl { get; init; }
    public string? CheckUrl { get; init; }

    /// <summary>True for an external (third-party) alert with no correlated check/service (RFC 0001).</summary>
    public bool IsExternal { get; init; }

    /// <summary>Display label for the alert's origin (e.g. "GCP Cloud Monitoring"), null for an internal alert.</summary>
    public string? SourceLabel { get; init; }

    /// <summary>Deep link into the source system's own console for this occurrence, if provided.</summary>
    public string? SourceUrl { get; init; }
}

/// <summary>An alert was opened (RFC 0009 <c>alert:created</c>).</summary>
public sealed record AlertCreatedEvent : AlertEvent
{
    public override string EventKey => "alert:created";
}

/// <summary>A team member acknowledged an active alert (RFC 0009 <c>alert:acknowledged</c>).</summary>
public sealed record AlertAcknowledgedEvent : AlertEvent
{
    public override string EventKey => "alert:acknowledged";
}

/// <summary>An active alert cleared/recovered (RFC 0009 <c>alert:resolved</c>).</summary>
public sealed record AlertResolvedEvent : AlertEvent
{
    public override string EventKey => "alert:resolved";
}
