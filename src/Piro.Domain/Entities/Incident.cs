using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A manually declared service disruption with a tracked investigation state.</summary>
public class Incident
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Unix timestamp (seconds) when the incident started.</summary>
    public long StartDateTime { get; set; }

    /// <summary>Unix timestamp (seconds) when the incident was resolved. Null if still active.</summary>
    public long? EndDateTime { get; set; }

    /// <inheritdoc cref="IsResolved"/>
    [Obsolete("Use IsResolved (derived from State) instead.")]
    public IncidentStatus Status { get; set; } = IncidentStatus.Active;

    public IncidentState State { get; set; } = IncidentState.Investigating;

    /// <summary>True when <see cref="State"/> is <see cref="IncidentState.Resolved"/>.</summary>
    public bool IsResolved => State == IncidentState.Resolved;

    /// <summary>When true, the incident affects all services regardless of <see cref="IncidentServices"/>.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Origin of the incident: MANUAL, WEBHOOK, or ALERT.</summary>
    public string? Source { get; set; }

    /// <summary>Unix timestamp (seconds) when a team member acknowledged the incident. Null if not yet acknowledged.</summary>
    public long? AcknowledgedAt { get; set; }

    /// <summary>Display name of the user who acknowledged the incident.</summary>
    public string? AcknowledgedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<IncidentComment> Comments { get; set; } = [];
    public ICollection<IncidentService> IncidentServices { get; set; } = [];
}
