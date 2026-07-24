namespace Piro.Contracts;

/// <summary>
/// Fields common to every incident-family event (RFC 0009 <c>incident:*</c>). An incident is not an
/// alert (no check/service/severity of its own); it has a title, a status, affected services, and a
/// visibility. Status and visibility are neutral display strings here, not the domain
/// <c>IncidentStatus</c>/<c>IncidentVisibility</c> enums, so an integration stays domain-isolated.
/// </summary>
public abstract record IncidentEvent : Event
{
    public int IncidentId { get; init; }

    /// <summary>Current incident status as a neutral display string (e.g. "Investigating", "Resolved").</summary>
    public string? Status { get; init; }

    /// <summary>Incident visibility as a neutral display string (e.g. "Public", "Internal").</summary>
    public string? Visibility { get; init; }

    /// <summary>Names of the services this incident affects.</summary>
    public IReadOnlyList<string> AffectedServices { get; init; } = [];
}

/// <summary>An incident was opened (RFC 0009 <c>incident:created</c>).</summary>
public sealed record IncidentCreatedEvent : IncidentEvent
{
    public override string EventKey => "incident:created";
}

/// <summary>An incident reached a final state, resolved or merged (RFC 0009 <c>incident:resolved</c>).</summary>
public sealed record IncidentResolvedEvent : IncidentEvent
{
    public override string EventKey => "incident:resolved";
}
