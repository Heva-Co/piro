namespace Piro.Domain.Entities;

/// <summary>
/// A one-off coverage override for an <see cref="OnCallSchedule"/>.
/// The override user covers the period [<see cref="StartsAtUtc"/>, <see cref="EndsAtUtc"/>).
/// If <see cref="ReplacesUserId"/> is set, that user is removed from the final schedule during this period.
/// If null, this is additional coverage (no replacement).
/// </summary>
public class OnCallOverride
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public OnCallSchedule Schedule { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>The user being replaced. Null means this override adds coverage without replacing anyone.</summary>
    public int? ReplacesUserId { get; set; }
    public AppUser? ReplacesUser { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string? Reason { get; set; }
}
