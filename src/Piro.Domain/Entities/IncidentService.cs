using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Junction between an <see cref="Incident"/> and an affected <see cref="Service"/>.</summary>
public class IncidentService
{
    public int IncidentId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Status impact on this service due to the incident.</summary>
    public ServiceStatus Impact { get; set; } = ServiceStatus.DOWN;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Incident Incident { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
