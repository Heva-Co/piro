namespace Piro.Domain.Enums;

/// <summary>
/// Derived, user-facing lifecycle status for a maintenance window — combines
/// <see cref="Maintenance.Status"/> (enabled/cancelled) with the temporal state
/// of its <see cref="MaintenanceEvent"/> occurrences.
/// </summary>
public enum MaintenanceDisplayStatus
{
    /// <summary>No event is currently ongoing, but one is scheduled in the future.</summary>
    Scheduled,
    /// <summary>At least one event is currently ongoing.</summary>
    Active,
    /// <summary>All events have completed and none remain scheduled or ongoing.</summary>
    Completed,
    /// <summary>The maintenance was cancelled.</summary>
    Cancelled
}
