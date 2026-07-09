using System.Threading.Channels;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>CRUD and event materialization for maintenance windows.</summary>
public class MaintenanceAppService(
    IMaintenanceRepository maintenanceRepo,
    IServiceRepository serviceRepo,
    IRRuleExpander rruleExpander,
    Channel<CheckStatusChangedEvent> statusChannel)
{
    /// <summary>Number of future days to materialize maintenance events on create/update.</summary>
    private const int MaterializeHorizonDays = 90;

    public async Task<IEnumerable<MaintenanceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await maintenanceRepo.GetAllAsync(ct);
        return list.Select(Map);
    }

    public async Task<MaintenanceDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var m = await maintenanceRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Maintenance), id.ToString());
        return Map(m);
    }

    public async Task<MaintenanceDto> CreateAsync(CreateMaintenanceRequest request, CancellationToken ct = default)
    {
        var maintenance = new Maintenance
        {
            Title = request.Title,
            Description = request.Description,
            StartDateTime = request.StartDateTime,
            RRule = request.RRule,
            DurationSeconds = request.DurationSeconds,
            IsGlobal = request.IsGlobal,
            Status = MaintenanceStatus.Active
        };

        if (request.ServiceSlugs is not null)
        {
            foreach (var slug in request.ServiceSlugs)
            {
                var service = await serviceRepo.GetBySlugAsync(slug, ct)
                    ?? throw new NotFoundException(nameof(Service), slug);
                maintenance.MaintenanceServices.Add(new MaintenanceService { ServiceId = service.Id });
            }
        }

        var created = await maintenanceRepo.CreateAsync(maintenance, ct);
        await MaterializeEventsAsync(created, ct);
        return Map(created);
    }

    public async Task<MaintenanceDto> UpdateAsync(int id, UpdateMaintenanceRequest request, CancellationToken ct = default)
    {
        var maintenance = await maintenanceRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Maintenance), id.ToString());

        if (request.Title is not null) maintenance.Title = request.Title;
        if (request.Description is not null) maintenance.Description = request.Description;
        if (request.IsGlobal.HasValue) maintenance.IsGlobal = request.IsGlobal.Value;

        var scheduleChanged =
            (request.StartDateTime.HasValue && request.StartDateTime.Value != maintenance.StartDateTime) ||
            (request.RRule is not null && request.RRule != maintenance.RRule) ||
            (request.DurationSeconds.HasValue && request.DurationSeconds.Value != maintenance.DurationSeconds);

        if (request.StartDateTime.HasValue) maintenance.StartDateTime = request.StartDateTime.Value;
        if (request.DurationSeconds.HasValue) maintenance.DurationSeconds = request.DurationSeconds.Value;
        if (request.RRule is not null) maintenance.RRule = request.RRule;

        if (scheduleChanged)
        {
            // Re-materialize all future (and currently-active) events from the new start
            await maintenanceRepo.DeleteFutureEventsAsync(id, maintenance.StartDateTime, ct);
            await MaterializeEventsAsync(maintenance, ct);
        }

        var updated = await maintenanceRepo.UpdateAsync(maintenance, ct);
        return Map(updated);
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        var maintenance = await maintenanceRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Maintenance), id.ToString());

        maintenance.Status = MaintenanceStatus.Cancelled;
        await maintenanceRepo.UpdateAsync(maintenance, ct);
        await maintenanceRepo.DeleteFutureEventsAsync(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var maintenance = await maintenanceRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Maintenance), id.ToString());
        await maintenanceRepo.DeleteAsync(maintenance, ct);
    }

    /// <summary>
    /// Cancels a single occurrence of a recurring maintenance without affecting the
    /// parent maintenance or its other events. If the event was ongoing, recomputes
    /// status for the services it affected so they stop reporting MAINTENANCE.
    /// </summary>
    public async Task CancelEventAsync(int maintenanceId, int eventId, CancellationToken ct = default)
    {
        var maintenanceEvent = await maintenanceRepo.GetEventByIdAsync(maintenanceId, eventId, ct)
            ?? throw new NotFoundException(nameof(MaintenanceEvent), eventId.ToString());

        var wasOngoing = maintenanceEvent.Status == MaintenanceEventStatus.Ongoing;
        await maintenanceRepo.CancelEventAsync(maintenanceEvent, ct);

        if (wasOngoing)
        {
            // Enqueue through the shared channel rather than calling ServiceStatusService
            // directly — StatusDrainHostedService serializes all recomputation for a
            // given service, avoiding concurrent read-modify-write races.
            var affectedServiceIds = await maintenanceRepo.GetAffectedServiceIdsAsync(maintenanceId, ct);
            foreach (var serviceId in affectedServiceIds)
                statusChannel.Writer.TryWrite(new CheckStatusChangedEvent(0, serviceId, ServiceStatus.NO_DATA, ServiceStatus.NO_DATA));
        }
    }

    // ── Event materialization ────────────────────────────────────────────────

    /// <summary>
    /// Generates <see cref="MaintenanceEvent"/> rows from the RRULE for the next
    /// <see cref="MaterializeHorizonDays"/> days and persists them.
    /// </summary>
    public async Task MaterializeEventsAsync(Maintenance maintenance, CancellationToken ct = default)
    {
        var dtStart = DateTimeOffset.FromUnixTimeSeconds(maintenance.StartDateTime).UtcDateTime;
        // Use dtStart (not UtcNow) as the window start so ongoing events (already started) are included
        var windowStart = dtStart < DateTime.UtcNow ? dtStart : DateTime.UtcNow;
        var occurrences = rruleExpander.GetOccurrences(dtStart, maintenance.RRule, windowStart, DateTime.UtcNow.AddDays(MaterializeHorizonDays));

        var events = occurrences.Select(start => new MaintenanceEvent
        {
            MaintenanceId = maintenance.Id,
            StartDateTime = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeSeconds(),
            EndDateTime = new DateTimeOffset(start.AddSeconds(maintenance.DurationSeconds), TimeSpan.Zero).ToUnixTimeSeconds(),
            Status = start <= DateTime.UtcNow ? MaintenanceEventStatus.Ongoing : MaintenanceEventStatus.Scheduled
        }).ToList();

        await maintenanceRepo.AddEventsAsync(events, ct);
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static MaintenanceDto Map(Maintenance m) => new(
        m.Id, m.Title, m.Description, m.StartDateTime, m.RRule,
        m.DurationSeconds, m.Status, DeriveDisplayStatus(m), m.IsGlobal,
        m.Events
            .Where(e => e.Status != MaintenanceEventStatus.Completed)
            .OrderBy(e => e.StartDateTime)
            .Take(10)
            .Select(e => new MaintenanceEventDto(e.Id, e.StartDateTime, e.EndDateTime, e.Status)),
        m.MaintenanceServices.Select(ms => ms.Service?.Slug ?? ms.ServiceId.ToString()),
        m.CreatedAt, m.UpdatedAt);

    /// <summary>Combines <see cref="Maintenance.Status"/> with event states into a single user-facing lifecycle status.</summary>
    private static MaintenanceDisplayStatus DeriveDisplayStatus(Maintenance m)
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
