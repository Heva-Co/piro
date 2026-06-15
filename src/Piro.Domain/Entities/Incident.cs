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

    public IncidentStatus Status { get; set; } = IncidentStatus.Active;
    public IncidentState State { get; set; } = IncidentState.Investigating;

    /// <summary>When true, the incident affects all services regardless of <see cref="IncidentServices"/>.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Origin of the incident: MANUAL, WEBHOOK, or ALERT.</summary>
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<IncidentComment> Comments { get; set; } = [];
    public ICollection<IncidentService> IncidentServices { get; set; } = [];
}
