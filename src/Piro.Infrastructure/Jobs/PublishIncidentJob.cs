using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// One-shot Quartz job that publishes a draft incident to the status page after the configured delay.
/// Cancelled automatically when the incident is manually published, deleted, or merged into a global incident.
/// </summary>
[DisallowConcurrentExecution]
public class PublishIncidentJob(IServiceScopeFactory scopeFactory, ILogger<PublishIncidentJob> logger) : IJob
{
    internal const string IncidentIdKey = "incidentId";

    public async Task Execute(IJobExecutionContext context)
    {
        if (!context.MergedJobDataMap.TryGetValue(IncidentIdKey, out var raw) ||
            !int.TryParse(raw?.ToString(), out var incidentId))
        {
            logger.LogWarning("PublishIncidentJob fired without a valid incidentId.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var incidentRepo = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

        var incident = await incidentRepo.GetByIdAsync(incidentId, context.CancellationToken);
        if (incident is null)
        {
            logger.LogWarning("PublishIncidentJob: incident #{Id} not found — skipping.", incidentId);
            return;
        }

        if (incident.IsPublic)
        {
            logger.LogDebug("PublishIncidentJob: incident #{Id} already public — skipping.", incidentId);
            return;
        }

        await incidentRepo.PublishAsync(incident, context.CancellationToken);
        logger.LogInformation("PublishIncidentJob: incident #{Id} published to status page.", incidentId);
    }
}
