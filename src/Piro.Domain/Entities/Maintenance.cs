using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>Scheduled maintenance window definition, supporting recurrence via RRULE.</summary>
public class Maintenance
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Unix timestamp (seconds) of the first occurrence start.</summary>
    public long StartDateTime { get; set; }

    /// <summary>iCalendar RRULE string defining recurrence (e.g. "FREQ=WEEKLY;BYDAY=MO").</summary>
    public string RRule { get; set; } = string.Empty;

    /// <summary>Duration of each maintenance window in seconds.</summary>
    public int DurationSeconds { get; set; }

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Active;

    /// <summary>When true, all services are affected regardless of <see cref="MaintenanceServices"/>.</summary>
    public bool IsGlobal { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MaintenanceEvent> Events { get; set; } = [];
    public ICollection<MaintenanceService> MaintenanceServices { get; set; } = [];
}
