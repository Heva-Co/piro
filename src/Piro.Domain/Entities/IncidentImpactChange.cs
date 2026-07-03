using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// Records a point-in-time change in the worst-case impact of an <see cref="Incident"/>.
/// Created automatically when checks are attached or detached via auto-create/close logic,
/// and can be created manually when an operator updates the incident impact.
/// The sequence of changes allows the public status page to reconstruct the exact severity
/// at any given minute for historical display.
/// </summary>
public class IncidentImpactChange
{
    public int Id { get; set; }
    public int IncidentId { get; set; }

    /// <summary>The new worst-case impact at this point in time.</summary>
    public ServiceStatus Impact { get; set; }

    /// <summary>Unix timestamp (seconds) when this impact level took effect.</summary>
    public long Timestamp { get; set; }

    public Incident Incident { get; set; } = null!;
}
