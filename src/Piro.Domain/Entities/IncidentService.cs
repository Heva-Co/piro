using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Junction between an <see cref="Incident"/> and an affected <see cref="Service"/>.</summary>
public class IncidentService
{
    public int IncidentId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Status impact on this service due to the incident.</summary>
    public ServiceStatus Impact { get; set; } = ServiceStatus.DOWN;

    /// <summary>The check that triggered this service being attached to the incident. Null for manually added services.</summary>
    public int? TriggeringCheckId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Incident Incident { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Check? TriggeringCheck { get; set; }
}
