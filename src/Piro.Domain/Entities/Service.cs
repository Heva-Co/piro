using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Represents a monitored application or infrastructure component.</summary>
/// <remarks>
/// A service owns one or more <see cref="Check"/> instances and may declare
/// dependencies on other services via <see cref="DependsOn"/>. Its
/// <see cref="CurrentStatus"/> is always computed — never set manually.
/// </remarks>
public class Service
{
    public int Id { get; set; }

    /// <summary>URL-safe unique identifier, e.g. "heva-backend".</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Derived status computed from checks and dependency propagation. Never set directly.</summary>
    public ServiceStatus CurrentStatus { get; set; } = ServiceStatus.NO_DATA;

    /// <summary>
    /// Status shown on the public status page. Defaults to UP — raw check failures never affect
    /// it. Only degraded/worsened by an active maintenance window or a Public incident's declared
    /// impact. Never set directly.
    /// </summary>
    public ServiceStatus PublicStatus { get; set; } = ServiceStatus.UP;

    /// <summary>Status to use when no check data is available yet.</summary>
    public ServiceStatus DefaultStatus { get; set; } = ServiceStatus.NO_DATA;

    /// <summary>When true, this service is excluded from the public status page.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Display position on the status page (ascending order).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Days of status history shown on desktop on the public page.</summary>
    public int HistoryDaysDesktop { get; set; } = 30;
    /// <summary>Days of status history shown on mobile on the public page.</summary>
    public int HistoryDaysMobile { get; set; } = 15;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Check> Checks { get; set; } = [];
    public ICollection<ServiceDependency> DependsOn { get; set; } = [];
    public ICollection<ServiceDependency> DependedOnBy { get; set; } = [];
    public ICollection<PageService> PageServices { get; set; } = [];
    public ICollection<IncidentService> IncidentServices { get; set; } = [];
    public ICollection<MaintenanceService> MaintenanceServices { get; set; } = [];
}
