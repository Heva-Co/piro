using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Application.Models;
using Piro.Application.Services;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// Background service that drains the <see cref="CheckStatusChangedEvent"/> channel
/// and triggers service status recomputation with cascade to downstream services.
/// </summary>
internal class StatusDrainHostedService(
    Channel<CheckStatusChangedEvent> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<StatusDrainHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in channel.Reader.ReadAllAsync(stoppingToken))
        {
            logger.LogDebug(
                "Check {CheckId} (service {ServiceId}) status changed: {Old} → {New}.",
                evt.CheckId, evt.ServiceId, evt.PreviousStatus, evt.NewStatus);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var statusService = scope.ServiceProvider.GetRequiredService<ServiceStatusService>();
                await statusService.ComputeAllWithCascadeAsync([evt.ServiceId], stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recomputing status for service {ServiceId}.", evt.ServiceId);
            }
        }
    }
}
