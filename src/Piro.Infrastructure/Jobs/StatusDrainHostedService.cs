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

                // This is the one sanctioned call site for ComputeAllWithCascadeAsync: as the
                // single consumer of `channel`, this loop processes one event at a time, which is
                // what actually prevents the concurrent read-modify-write race the method warns
                // about. Any other caller must enqueue onto `channel` instead — see the method's
                // XML doc remarks for why.
#pragma warning disable CS0618
                await statusService.ComputeAllWithCascadeAsync([evt.ServiceId], stoppingToken);
#pragma warning restore CS0618
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recomputing status for service {ServiceId}.", evt.ServiceId);
            }
        }
    }
}
