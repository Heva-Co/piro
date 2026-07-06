namespace Piro.Domain.Entities;

/// <summary>Defines who is on-call over time using rotation layers and overrides.</summary>
public class OnCallSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>IANA timezone identifier (e.g. "America/New_York"). Used for display only — all datetimes stored in UTC.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>When true, notify the on-call user when their shift begins.</summary>
    public bool NotifyOnShiftStart { get; set; }

    /// <summary>Optional start of the schedule's active window. Null = always active from beginning.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    /// <summary>Optional end of the schedule's active window. Null = never expires.</summary>
    public DateTimeOffset? EndsAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<OnCallLayer> Layers { get; set; } = [];
    public ICollection<OnCallOverride> Overrides { get; set; } = [];
}
