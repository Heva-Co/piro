using Piro.Application.DTOs;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

public static class MaintenanceExtensions
{
    public static MaintenanceDto ToDto(this Maintenance m) => new(
        m.Id, m.Title, m.Description, m.StartDateTime, m.RRule,
        m.DurationSeconds, m.Status, m.DeriveDisplayStatus(), m.IsGlobal,
        m.Events
            .Where(e => e.Status != MaintenanceEventStatus.Completed)
            .OrderBy(e => e.StartDateTime)
            .Take(10)
            .Select(e => new MaintenanceEventDto(e.Id, e.StartDateTime, e.EndDateTime, e.Status)),
        m.MaintenanceServices.Select(ms => ms.Service?.Slug ?? ms.ServiceId.ToString()),
        m.CreatedAt, m.UpdatedAt);

    /// <summary>
    /// Maps a maintenance for the public status page. Unlike <see cref="ToDto"/>, this does not
    /// drop Completed events — the public page needs the real current/most-recent event (even if
    /// already finished) rather than only what's still pending, or it falls back to a misleading
    /// default status when nothing is left "upcoming". Prefers the Ongoing event if there is one,
    /// otherwise the soonest Scheduled event, otherwise the most recently Completed one — never
    /// the furthest-out future occurrence of a recurring maintenance.
    /// </summary>
    public static MaintenanceDto ToPublicDto(this Maintenance m) => new(
        m.Id, m.Title, m.Description, m.StartDateTime, m.RRule,
        m.DurationSeconds, m.Status, m.DeriveDisplayStatus(), m.IsGlobal,
        PublicRelevantEvent(m).Select(e => new MaintenanceEventDto(e.Id, e.StartDateTime, e.EndDateTime, e.Status)),
        m.MaintenanceServices.Select(ms => ms.Service?.Slug ?? ms.ServiceId.ToString()),
        m.CreatedAt, m.UpdatedAt);

    private static IEnumerable<MaintenanceEvent> PublicRelevantEvent(Maintenance m)
    {
        var ongoing = m.Events.FirstOrDefault(e => e.Status == MaintenanceEventStatus.Ongoing);
        if (ongoing is not null) return [ongoing];

        var nextScheduled = m.Events
            .Where(e => e.Status == MaintenanceEventStatus.Scheduled)
            .OrderBy(e => e.StartDateTime)
            .FirstOrDefault();
        if (nextScheduled is not null) return [nextScheduled];

        var mostRecentlyDone = m.Events
            .OrderByDescending(e => e.StartDateTime)
            .FirstOrDefault();
        return mostRecentlyDone is not null ? [mostRecentlyDone] : [];
    }

    public static MaintenanceListItemDto ToListItemDto(this Maintenance m) => new(
        m.Id, m.Title, m.RRule, m.DurationSeconds, m.DeriveDisplayStatus(), m.IsGlobal,
        m.Events
            .Where(e => e.Status == MaintenanceEventStatus.Scheduled)
            .OrderBy(e => e.StartDateTime)
            .Select(e => (long?)e.StartDateTime)
            .FirstOrDefault(),
        m.MaintenanceServices.Select(ms => ms.Service?.Slug ?? ms.ServiceId.ToString()));

    /// <summary>Combines <see cref="Maintenance.Status"/> with event states into a single user-facing lifecycle status.</summary>
    public static MaintenanceDisplayStatus DeriveDisplayStatus(this Maintenance m)
    {
        if (m.Status == MaintenanceStatus.Cancelled)
            return MaintenanceDisplayStatus.Cancelled;

        if (m.Events.Any(e => e.Status == MaintenanceEventStatus.Ongoing))
            return MaintenanceDisplayStatus.Active;

        if (m.Events.Any(e => e.Status == MaintenanceEventStatus.Scheduled))
            return MaintenanceDisplayStatus.Scheduled;

        return MaintenanceDisplayStatus.Completed;
    }
}
