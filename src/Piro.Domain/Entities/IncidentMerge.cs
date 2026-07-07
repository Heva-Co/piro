namespace Piro.Domain.Entities;

/// <summary>Records when a per-service incident was absorbed into a global (correlated) incident.</summary>
public class IncidentMerge
{
    public int Id { get; set; }

    /// <summary>The per-service incident being absorbed.</summary>
    public int SourceIncidentId { get; set; }

    /// <summary>The global incident absorbing it.</summary>
    public int TargetIncidentId { get; set; }

    public DateTimeOffset MergedAt { get; set; }

    /// <summary>Auto-set to "Automatic correlation" for system merges; supports manual merge reason in the future.</summary>
    public string? Reason { get; set; }

    public Incident SourceIncident { get; set; } = null!;
    public Incident TargetIncident { get; set; } = null!;
}
