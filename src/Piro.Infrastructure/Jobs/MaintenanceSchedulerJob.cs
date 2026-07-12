using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Enums;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// Runs every 15 minutes to:
/// 1. Advance Scheduled → Ongoing and Ongoing → Completed event statuses.
/// 2. Extend the materialization horizon for active maintenances when it falls below 30 days.
/// </summary>
[DisallowConcurrentExecution]
public class MaintenanceSchedulerJob(
    IServiceScopeFactory scopeFactory,
    Channel<CheckStatusChangedEvent> statusChannel,
    ILogger<MaintenanceSchedulerJob> logger) : IJob
{
    public static readonly JobKey Key = new("maintenance-scheduler", "piro");

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var maintenanceRepo = scope.ServiceProvider.GetRequiredService<IMaintenanceRepository>();
        var maintenanceService = scope.ServiceProvider.GetRequiredService<MaintenanceAppService>();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var db = scope.ServiceProvider.GetRequiredService<Piro.Infrastructure.Persistence.PiroDbContext>();

        // Transition Scheduled → Ongoing
        var startingEvents = db.MaintenanceEvents
            .Where(e => e.Status == MaintenanceEventStatus.Scheduled && e.StartDateTime <= now)
            .ToList();
        foreach (var ev in startingEvents)
            ev.Status = MaintenanceEventStatus.Ongoing;

        // Transition Ongoing → Completed
        var completedEvents = db.MaintenanceEvents
            .Where(e => e.Status == MaintenanceEventStatus.Ongoing && e.EndDateTime < now)
            .ToList();
        foreach (var ev in completedEvents)
            ev.Status = MaintenanceEventStatus.Completed;

        var transitionedEvents = startingEvents.Concat(completedEvents).ToList();
        if (transitionedEvents.Count > 0)
        {
            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Updated {Starting} event(s) to Ongoing, {Completed} to Completed.",
                startingEvents.Count, completedEvents.Count);

            var maintenanceIds = transitionedEvents.Select(e => e.MaintenanceId).Distinct().ToList();
            var affectedServiceIds = await maintenanceRepo.GetAffectedServiceIdsAsync(maintenanceIds, context.CancellationToken);

            // Enqueue through the same channel CheckResultIngesterService uses, rather than calling
            // ServiceStatusService directly — StatusDrainHostedService is the single consumer that
            // serializes all recomputation for a given service, avoiding concurrent read-modify-write.
            foreach (var serviceId in affectedServiceIds)
                statusChannel.Writer.TryWrite(new CheckStatusChangedEvent(0, serviceId, ServiceStatus.NO_DATA, ServiceStatus.NO_DATA));
        }

        // Extend horizon: materialize more events when nearest future event < 30 days out
        var activeMaint = await maintenanceRepo.GetActiveAsync(context.CancellationToken);
        var horizonThreshold = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var extendedAny = false;
        foreach (var m in activeMaint)
        {
            var latestEvent = await db.MaintenanceEvents
                .Where(e => e.MaintenanceId == m.Id)
                .OrderByDescending(e => e.StartDateTime)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (latestEvent is null || latestEvent.StartDateTime < horizonThreshold)
            {
                var addedCount = await maintenanceService.MaterializeEventsAsync(m, context.CancellationToken);
                if (addedCount > 0)
                {
                    extendedAny = true;
                    logger.LogInformation("Extended materialization horizon for maintenance {Id} ({Count} new event(s)).", m.Id, addedCount);
                }
                // addedCount == 0 means the RRULE has no more occurrences to give (e.g. a one-time
                // COUNT=1 maintenance whose single event is already materialized and in the past) —
                // nothing to log, and this maintenance will keep hitting this branch forever since
                // its latest event never moves forward. That's a known limitation, not a bug to chase
                // here: without an "exhausted" flag on Maintenance, we can't skip it on future runs,
                // but the no-op call itself is cheap (one query, no writes).
            }
        }

        if (transitionedEvents.Count == 0 && !extendedAny)
            logger.LogInformation("No work to do, skipping.");
    }
}
