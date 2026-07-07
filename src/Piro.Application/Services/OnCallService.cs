using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>
/// Resolves who is on-call at a given moment using RRULE-based layer rotations and overrides.
/// All times are UTC. Duration per occurrence = FirstOccurrenceEndsAt − FirstOccurrenceStartsAt.
/// </summary>
public class OnCallService(IOnCallScheduleRepository scheduleRepo, IRRuleExpander rruleExpander)
{
    /// <summary>Returns the users currently on-call for the given schedule.</summary>
    public Task<IReadOnlyList<AppUser>> GetCurrentOnCallUsersAsync(int scheduleId, CancellationToken ct = default)
        => GetOnCallUsersAtAsync(scheduleId, DateTimeOffset.UtcNow, ct);

    public async Task<IReadOnlyList<AppUser>> GetOnCallUsersAtAsync(
        int scheduleId, DateTimeOffset at, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct);
        if (schedule is null) return [];

        // Check schedule active window
        if (schedule.StartsAtUtc.HasValue && at < schedule.StartsAtUtc) return [];
        if (schedule.EndsAtUtc.HasValue && at > schedule.EndsAtUtc) return [];

        var atUtc = at.UtcDateTime;

        // Active overrides at `at`
        var activeOverrides = schedule.Overrides
            .Where(o => o.StartsAtUtc <= at && at < o.EndsAtUtc)
            .ToList();

        var resolved = new Dictionary<int, AppUser>(); // userId → user

        foreach (var layer in schedule.Layers.OrderBy(l => l.Order))
        {
            if (!layer.Users.Any()) continue;

            var users = layer.Users.OrderBy(u => u.Position).ToList();
            var duration = layer.FirstOccurrenceEndsAt - layer.FirstOccurrenceStartsAt;
            var dtStart = layer.FirstOccurrenceStartsAt.UtcDateTime;

            // Find occurrences that could overlap `at` — look back one interval before `at`
            var lookbackFrom = atUtc - duration - TimeSpan.FromDays(1);
            var occurrences = rruleExpander.GetOccurrences(dtStart, layer.RecurrenceRule, lookbackFrom, atUtc);

            // Find the last occurrence that started before or at `at` and whose end covers `at`
            AppUser? layerUser = null;
            int elapsedIntervals = 0;

            foreach (var occ in occurrences.OrderBy(o => o))
            {
                var occEnd = occ + duration;
                if (occ <= atUtc && atUtc < occEnd)
                {
                    // Calculate how many full intervals have elapsed from the first occurrence
                    var intervalDuration = layer.RecurrenceRule.Contains("FREQ=DAILY", StringComparison.OrdinalIgnoreCase)
                        ? TimeSpan.FromDays(1)
                        : duration; // fallback to occurrence duration for custom RRULEs

                    var totalElapsed = occ - dtStart;
                    elapsedIntervals = duration.TotalSeconds > 0
                        ? (int)(totalElapsed.TotalSeconds / duration.TotalSeconds)
                        : 0;

                    var slotIndex = users.Count > 0 ? elapsedIntervals % users.Count : 0;
                    layerUser = users[slotIndex].User;
                    break;
                }
            }

            if (layerUser is null) continue;

            // Apply overrides for this layer
            var replacingOverride = activeOverrides.FirstOrDefault(o => o.ReplacesUserId == layerUser.Id);
            if (replacingOverride is not null)
            {
                // Replace this user with the override user
                resolved.TryAdd(replacingOverride.UserId, replacingOverride.User);
            }
            else
            {
                resolved.TryAdd(layerUser.Id, layerUser);
            }
        }

        // Additional coverage overrides (ReplacesUserId == null) — add without replacing
        foreach (var addOverride in activeOverrides.Where(o => o.ReplacesUserId is null))
        {
            resolved.TryAdd(addOverride.UserId, addOverride.User);
        }

        return resolved.Values.ToList();
    }

    /// <summary>
    /// Expands the schedule into concrete on-call slots for Gantt rendering.
    /// Returns slots for each layer + a merged "final schedule" view.
    /// </summary>
    /// <summary>
    /// Expands rotation slots for Gantt rendering.
    /// applyOverrides=false → pure rotation (no override substitution, used for the Rotations section).
    /// applyOverrides=true  → overrides applied, plus override-only slots appended (used for Final Schedule and Overrides section).
    /// </summary>
    public async Task<List<OnCallSlotDto>> ExpandScheduleAsync(
        int scheduleId, DateTimeOffset from, DateTimeOffset to,
        bool applyOverrides = true,
        CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdWithLayersAsync(scheduleId, ct);
        if (schedule is null) return [];

        var slots = new List<OnCallSlotDto>();
        var overrides = applyOverrides
            ? schedule.Overrides.Where(o => o.EndsAtUtc > from && o.StartsAtUtc < to).ToList()
            : [];

        foreach (var layer in schedule.Layers.OrderBy(l => l.Order))
        {
            if (!layer.Users.Any()) continue;

            var users = layer.Users.OrderBy(u => u.Position).ToList();
            var duration = layer.FirstOccurrenceEndsAt - layer.FirstOccurrenceStartsAt;
            var dtStart = layer.FirstOccurrenceStartsAt.UtcDateTime;

            var occurrences = rruleExpander.GetOccurrences(dtStart, layer.RecurrenceRule, from.UtcDateTime, to.UtcDateTime);

            // Count occurrences before `from` so rotation index is correct regardless of window start
            var occsBefore = rruleExpander.GetOccurrences(dtStart, layer.RecurrenceRule, dtStart, from.UtcDateTime).Count();
            int occIndex = occsBefore;
            foreach (var occ in occurrences.OrderBy(o => o))
            {
                var occStart = new DateTimeOffset(occ, TimeSpan.Zero);
                var occEnd = occStart + duration;

                var slotStart = occStart < from ? from : occStart;
                var slotEnd = occEnd > to ? to : occEnd;
                if (slotStart >= slotEnd) { occIndex++; continue; }

                var slotIndex = users.Count > 0 ? occIndex % users.Count : 0;
                var user = users[slotIndex].User;

                if (applyOverrides)
                {
                    var replacingOverride = overrides.FirstOrDefault(o =>
                        o.ReplacesUserId == user.Id &&
                        o.StartsAtUtc < slotEnd &&
                        o.EndsAtUtc > slotStart);

                    if (replacingOverride is not null)
                    {
                        // Emit the original-user segment before the override starts (if any)
                        if (slotStart < replacingOverride.StartsAtUtc)
                            AddLayerSlot(slots, layer, user, slotStart, replacingOverride.StartsAtUtc);

                        // Replace this occurrence's slot with the override user (clipped to occurrence bounds)
                        var overrideSlotStart = replacingOverride.StartsAtUtc < slotStart ? slotStart : replacingOverride.StartsAtUtc;
                        var overrideSlotEnd   = replacingOverride.EndsAtUtc > slotEnd ? slotEnd : replacingOverride.EndsAtUtc;
                        if (overrideSlotStart < overrideSlotEnd)
                            AddLayerSlot(slots, layer, replacingOverride.User, overrideSlotStart, overrideSlotEnd,
                                isOverride: true, replacesUserName: user.Name);

                        // Emit the original-user segment after the override ends (if any)
                        if (replacingOverride.EndsAtUtc < slotEnd)
                            AddLayerSlot(slots, layer, user, replacingOverride.EndsAtUtc, slotEnd);
                    }
                    else
                    {
                        AddLayerSlot(slots, layer, user, slotStart, slotEnd);
                    }
                }
                else
                {
                    AddLayerSlot(slots, layer, user, slotStart, slotEnd);
                }

                occIndex++;
            }
        }

        if (applyOverrides)
        {
            // Additional coverage overrides (no replacement)
            foreach (var o in overrides.Where(o => o.ReplacesUserId is null))
            {
                var slotStart = o.StartsAtUtc < from ? from : o.StartsAtUtc;
                var slotEnd = o.EndsAtUtc > to ? to : o.EndsAtUtc;
                if (slotStart >= slotEnd) continue;

                slots.Add(new OnCallSlotDto(
                    LayerId: 0,
                    LayerName: "Override",
                    UserId: o.UserId,
                    UserName: o.User.Name,
                    UserInitials: GetInitials(o.User.Name),
                    UserColor: o.User.Color,
                    StartsAt: slotStart,
                    EndsAt: slotEnd,
                    IsOverride: true,
                    ReplacesUserName: null));
            }
        }

        return slots;
    }

    private static void AddLayerSlot(
        List<OnCallSlotDto> slots, OnCallLayer layer, AppUser user,
        DateTimeOffset start, DateTimeOffset end,
        bool isOverride = false, string? replacesUserName = null)
    {
        slots.Add(new OnCallSlotDto(
            LayerId: layer.Id,
            LayerName: layer.Name,
            UserId: user.Id,
            UserName: user.Name,
            UserInitials: GetInitials(user.Name),
            UserColor: user.Color,
            StartsAt: start,
            EndsAt: end,
            IsOverride: isOverride,
            ReplacesUserName: replacesUserName));
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

