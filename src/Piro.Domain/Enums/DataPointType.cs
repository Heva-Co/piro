namespace Piro.Domain.Enums;

/// <summary>Indicates the origin of a <see cref="Entities.CheckDataPoint"/>.</summary>
public enum DataPointType
{
    /// <summary>Result of an actual check execution by a worker.</summary>
    REALTIME,

    /// <summary>Check was in a maintenance window; execution was skipped.</summary>
    MAINTENANCE,

    /// <summary>Synthetic point created when a manual incident is recorded.</summary>
    INCIDENT,

    /// <summary>Filler point for minutes with no data.</summary>
    DEFAULT,

    /// <summary>
    /// No worker was connected when the check was scheduled.
    /// The gap reflects monitor unavailability, not service downtime.
    /// </summary>
    MONITOR_OUTAGE,

    /// <summary>
    /// Workers were connected, but none matched the check's required worker tags, so the check could not
    /// be placed (RFC 0008 Part B, §4.6). Unlike <see cref="MONITOR_OUTAGE"/> (a transient gap that heals
    /// when a worker returns), this is a configuration error: the required tags are impossible or typo'd,
    /// and it clears only when the check is edited. Not service downtime; kept out of uptime math.
    /// </summary>
    UNSCHEDULABLE,
}
