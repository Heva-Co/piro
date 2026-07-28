using Piro.Domain.Enums;
using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>Junction between a <see cref="Maintenance"/> and an affected <see cref="Service"/>.</summary>
public class MaintenanceService : IAuditable
{
    public int MaintenanceId { get; set; }
    public int ServiceId { get; set; }
    public ServiceStatus Impact { get; set; } = ServiceStatus.MAINTENANCE;
    public DateTime CreatedAt { get; set; }

    public Maintenance Maintenance { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
