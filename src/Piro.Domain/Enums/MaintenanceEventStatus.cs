namespace Piro.Domain.Enums;

/// <summary>Status of a materialized maintenance occurrence.</summary>
public enum MaintenanceEventStatus
{
    Scheduled,
    Ongoing,
    Completed,
    Cancelled
}
