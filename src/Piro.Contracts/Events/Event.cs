namespace Piro.Contracts;

/// <summary>
/// A notification event as an integration sees it (RFC 0016), the neutral contract-layer replacement
/// for the core's <c>AlertNotificationContext</c>/<c>IncidentNotificationContext</c>. Each catalog
/// event (RFC 0009) is a concrete subtype (<see cref="AlertCreatedEvent"/>, etc.) — the event's
/// <em>type</em> carries what a flag like "IsRecovery" used to, and a dispatcher pattern-matches on
/// the subtype when it needs specifics. References no Piro.Domain type: severity is
/// <see cref="EventSeverity"/>, status is a plain string, so an integration assembly stays isolated
/// from the domain model. The core maps its own context onto the right subtype at the edge.
/// </summary>
public abstract record Event
{
    /// <summary>Stable wire name of this event in the catalog (RFC 0009), e.g. "alert:created".</summary>
    public abstract string EventKey { get; }

    /// <summary>Severity, neutral to the domain — see <see cref="EventSeverity"/>.</summary>
    public required EventSeverity Severity { get; init; }

    public DateTimeOffset FiredAt { get; init; }

    /// <summary><see cref="FiredAt"/> pre-formatted in the recipient's time zone (built per-recipient by the core).</summary>
    public string? FiredAtDisplay { get; init; }

    /// <summary>A one-line human title/subject for the event, built by the core.</summary>
    public required string Title { get; init; }

    /// <summary>Absolute admin URL most relevant to this event (alert/incident detail), if the site URL is configured.</summary>
    public string? Url { get; init; }
}
