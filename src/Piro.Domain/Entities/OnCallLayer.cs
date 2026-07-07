namespace Piro.Domain.Entities;

/// <summary>
/// A single rotation layer within an <see cref="OnCallSchedule"/>.
/// Active periods are defined by an RRULE recurrence pattern starting at <see cref="FirstOccurrenceStartsAt"/>.
/// Duration per occurrence = <see cref="FirstOccurrenceEndsAt"/> − <see cref="FirstOccurrenceStartsAt"/>.
/// </summary>
public class OnCallLayer
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public OnCallSchedule Schedule { get; set; } = null!;
    public string Name { get; set; } = string.Empty;

    /// <summary>Display order within the schedule (ascending). Lower = rendered first.</summary>
    public int Order { get; set; }

    /// <summary>iCalendar RRULE string, e.g. FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR</summary>
    public string RecurrenceRule { get; set; } = string.Empty;

    /// <summary>Start of the first occurrence (UTC).</summary>
    public DateTimeOffset FirstOccurrenceStartsAt { get; set; }

    /// <summary>End of the first occurrence (UTC). May cross midnight.</summary>
    public DateTimeOffset FirstOccurrenceEndsAt { get; set; }

    public ICollection<OnCallLayerUser> Users { get; set; } = [];
}
