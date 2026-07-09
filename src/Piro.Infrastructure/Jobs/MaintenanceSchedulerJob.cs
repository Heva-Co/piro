using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
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
    ILogger<MaintenanceSchedulerJob> logger) : IJob
{
    public static readonly JobKey Key = new("maintenance-scheduler", "piro");

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var maintenanceRepo = scope.ServiceProvider.GetRequiredService<IMaintenanceRepository>();
        var maintenanceService = scope.ServiceProvider.GetRequiredService<MaintenanceAppService>();
        var statusService = scope.ServiceProvider.GetRequiredService<ServiceStatusService>();

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

            var affectedServiceIds = new HashSet<int>();
            foreach (var maintenanceId in transitionedEvents.Select(e => e.MaintenanceId).Distinct())
                foreach (var serviceId in await maintenanceRepo.GetAffectedServiceIdsAsync(maintenanceId, context.CancellationToken))
                    affectedServiceIds.Add(serviceId);

            if (affectedServiceIds.Count > 0)
                await statusService.ComputeAllWithCascadeAsync(affectedServiceIds, context.CancellationToken);
        }

        // Extend horizon: materialize more events when nearest future event < 30 days out
        var activeMaint = await maintenanceRepo.GetActiveAsync(context.CancellationToken);
        var horizonThreshold = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        foreach (var m in activeMaint)
        {
            var latestEvent = db.MaintenanceEvents
                .Where(e => e.MaintenanceId == m.Id && e.Status == MaintenanceEventStatus.Scheduled)
                .OrderByDescending(e => e.StartDateTime)
                .FirstOrDefault();

            if (latestEvent is null || latestEvent.StartDateTime < horizonThreshold)
            {
                await maintenanceService.MaterializeEventsAsync(m, context.CancellationToken);
                logger.LogInformation("Extended materialization horizon for maintenance {Id}.", m.Id);
            }
        }
    }
}
