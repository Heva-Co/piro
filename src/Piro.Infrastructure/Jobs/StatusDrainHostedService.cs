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

            await RecomputeWithCascadeAsync(evt.ServiceId, stoppingToken);
        }
    }

    /// <summary>
    /// Recomputes the status for <paramref name="serviceId"/> and recursively
    /// cascades to any downstream services whose status may be affected.
    /// Uses a queue to avoid deep recursion on wide dependency graphs.
    /// </summary>
    private async Task RecomputeWithCascadeAsync(int rootServiceId, CancellationToken ct)
    {
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        queue.Enqueue(rootServiceId);

        while (queue.Count > 0)
        {
            var serviceId = queue.Dequeue();
            if (!visited.Add(serviceId)) continue;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var statusService = scope.ServiceProvider.GetRequiredService<ServiceStatusService>();

                var downstream = await statusService.ComputeAsync(serviceId, ct);
                foreach (var id in downstream)
                    if (!visited.Contains(id))
                        queue.Enqueue(id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recomputing status for service {ServiceId}.", serviceId);
            }
        }
    }
}
