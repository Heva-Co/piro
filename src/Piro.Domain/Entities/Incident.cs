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

    /// <summary>When false, the incident is a draft not visible on the public status page. Auto-created incidents start as drafts when a publish delay is configured.</summary>
    public bool IsPublic { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Denormalized worst-case impact — mirrors the most recent <see cref="IncidentImpactChange.Impact"/>.
    /// Updated automatically whenever a new ImpactChange is recorded.
    /// </summary>
    public ServiceStatus CurrentImpact { get; set; } = ServiceStatus.DOWN;

    public ICollection<IncidentComment> Comments { get; set; } = [];
    public ICollection<IncidentService> IncidentServices { get; set; } = [];
    public ICollection<IncidentMerge> MergesAsSource { get; set; } = [];
    public ICollection<IncidentMerge> MergesAsTarget { get; set; } = [];
    public ICollection<IncidentImpactChange> ImpactChanges { get; set; } = [];
}
