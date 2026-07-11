using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class OnCallScheduleExtensions
{
    /// <summary>Maps an <see cref="OnCallSchedule"/> entity to its outbound DTO representation.</summary>
    public static OnCallScheduleDto ToDto(this OnCallSchedule s) => new(
        s.Id, s.Name, s.Description, s.TimeZone, s.NotifyOnShiftStart,
        s.StartsAtUtc, s.EndsAtUtc, s.CreatedAt, s.UpdatedAt,
        s.Layers.OrderBy(l => l.Order).Select(l => l.ToDto()).ToList());

    /// <summary>Maps an <see cref="OnCallLayer"/> entity to its outbound DTO representation.</summary>
    public static OnCallLayerDto ToDto(this OnCallLayer l) => new(
        l.Id, l.ScheduleId, l.Name, l.Order, l.RecurrenceRule,
        l.FirstOccurrenceStartsAt, l.FirstOccurrenceEndsAt,
        IsAllDay(l.FirstOccurrenceStartsAt, l.FirstOccurrenceEndsAt),
        l.Users.OrderBy(u => u.Position).Select(u => new OnCallLayerUserDto(
            u.Id, u.UserId, u.User?.Name ?? string.Empty,
            GetInitials(u.User?.Name ?? string.Empty),
            u.User?.Color ?? "#6366f1",
            u.Position)).ToList());

    /// <summary>Maps an <see cref="OnCallOverride"/> entity to its outbound DTO representation.</summary>
    public static OnCallOverrideDto ToDto(this OnCallOverride o) => new(
        o.Id, o.ScheduleId, o.UserId,
        o.User?.Name ?? string.Empty,
        o.User?.Color ?? "#6366f1",
        o.ReplacesUserId, o.ReplacesUser?.Name,
        o.StartsAtUtc, o.EndsAtUtc, o.Reason);

    private static bool IsAllDay(DateTimeOffset start, DateTimeOffset end)
    {
        var s = start.ToUniversalTime();
        var e = end.ToUniversalTime();
        return s.TimeOfDay == TimeSpan.Zero
            && (e.TimeOfDay == new TimeSpan(23, 59, 59) || e.TimeOfDay == new TimeSpan(23, 59, 0));
    }

    private static string GetInitials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }
}
