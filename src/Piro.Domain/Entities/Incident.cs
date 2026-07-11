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

    public IncidentStatus Status { get; set; } = IncidentStatus.Investigating;

    /// <summary>True when the incident has reached a final state — <see cref="IncidentStatus.Resolved"/>
    /// or <see cref="IncidentStatus.Merged"/> — and no further updates/acks/impact changes apply.</summary>
    public bool IsResolved => Status is IncidentStatus.Resolved or IncidentStatus.Merged;

    /// <summary>Origin of the incident: MANUAL, WEBHOOK, or ALERT.</summary>
    public string? Source { get; set; }

    /// <summary>Unix timestamp (seconds) when a team member acknowledged the incident. Null if not yet acknowledged.</summary>
    public long? AcknowledgedAt { get; set; }

    /// <summary>Display name of the user who acknowledged the incident.</summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>Controls visibility on the public status page. All incidents start Private — publishing is always an explicit, manual action.</summary>
    public IncidentVisibility Visibility { get; set; } = IncidentVisibility.Private;

    /// <summary>True when <see cref="Visibility"/> is <see cref="IncidentVisibility.Public"/>.</summary>
    public bool IsPublic => Visibility == IncidentVisibility.Public;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Denormalized worst-case impact — mirrors the most recent <see cref="IncidentImpactChange.Impact"/>.
    /// Updated automatically whenever a new ImpactChange is recorded.
    /// </summary>
    public ServiceStatus CurrentImpact { get; set; } = ServiceStatus.DOWN;

    public ICollection<IncidentTimelineEvent> TimelineEvents { get; set; } = [];
    public ICollection<IncidentService> IncidentServices { get; set; } = [];
    public ICollection<IncidentMerge> MergesAsSource { get; set; } = [];
    public ICollection<IncidentMerge> MergesAsTarget { get; set; } = [];
    public ICollection<IncidentImpactChange> ImpactChanges { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];

    // Escalation
    public int? EscalationPolicyId { get; set; }
    public EscalationPolicy? EscalationPolicy { get; set; }
    /// <summary>0-based index of the current escalation step. Null = not yet initialized.</summary>
    public int? EscalationCurrentStep { get; set; }
    /// <summary>When the current step became active. Step 0 uses incident.CreatedAt; subsequent steps use dispatch time.</summary>
    public DateTimeOffset? EscalationStepStartedAt { get; set; }
    /// <summary>Updated when a human adds a comment or manually updates the incident. Used for inactivity re-escalation.</summary>
    public DateTimeOffset? LastUserActivityAt { get; set; }
}
