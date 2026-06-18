using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.Worker;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Hubs;

/// <summary>
/// SignalR hub that remote check workers connect to.
/// Authentication is token-based: the worker passes its token via the
/// <c>access_token</c> query parameter on connection.
/// </summary>
public class WorkerHub(
    IWorkerRegistry registry,
    IWorkerRegistrationRepository workerRepo,
    ICheckResultIngester ingester,
    IMultiRegionBatchTracker batchTracker,
    IHubContext<AdminHub, IAdminClient> adminHub,
    ILogger<WorkerHub> logger) : Hub<IWorkerClient>
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        // The .NET SignalR client sends the token as "Authorization: Bearer <token>" (HTTP header).
        // Some transports / older clients may use the "access_token" query param instead.
        // Support both so the worker always connects regardless of transport negotiation.
        string? token = httpContext?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = httpContext?.Request.Headers.Authorization.ToString();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                token = authHeader["Bearer ".Length..];
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Worker connection rejected — no worker token provided. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var tokenHash = HashToken(token);
        var registration = await workerRepo.FindByWorkerTokenHashAsync(tokenHash);

        if (registration is null)
        {
            logger.LogWarning("Worker connection rejected — invalid worker token. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        Context.Items["WorkerRegistrationId"] = registration.Id;
        Context.Items["WorkerRegion"] = registration.Region;

        registry.Register(Context.ConnectionId, new WorkerInfo(
            registration.Id,
            Context.ConnectionId,
            registration.Region,
            DateTime.UtcNow,
            DateTime.UtcNow,
            IsDefault: registration.IsDefault));

        registration.LastHeartbeat = DateTime.UtcNow;
        await workerRepo.UpdateAsync(registration);

        logger.LogInformation("Worker connected. WorkerId={WorkerId} Region={Region} ConnectionId={ConnectionId}",
            registration.Id, registration.Region, Context.ConnectionId);

        await Clients.Caller.Ack(new WorkerAckMessage(registration.Id, registration.Region));
        await adminHub.Clients.All.WorkersChanged();
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Unregister(Context.ConnectionId);

        if (Context.Items.TryGetValue("WorkerRegistrationId", out var id))
            logger.LogInformation("Worker disconnected. WorkerId={WorkerId} ConnectionId={ConnectionId}", id, Context.ConnectionId);

        _ = adminHub.Clients.All.WorkersChanged();
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Sent by the worker after completing a check execution.</summary>
    public async Task Result(WorkerResultMessage message)
    {
        var ct = Context.ConnectionAborted;
        var region = Context.Items["WorkerRegion"] as string ?? "default";
        var status = Enum.Parse<ServiceStatus>(message.Status, ignoreCase: true);
        var result = new CheckExecutionResult(status, message.LatencyMs, message.ErrorMessage);

        try
        {
            if (!string.IsNullOrEmpty(message.BatchId))
            {
                // Multi-region path: persist per-region datapoint, then let the batch tracker
                // aggregate all results and call IngestStatusOnlyAsync exactly once.
                await ingester.IngestDataPointOnlyAsync(message.CheckId, result, region, ct);
                batchTracker.AddResult(message.BatchId, result);
            }
            else
            {
                // Single-region path: full ingestion (datapoint + status + alerts).
                await ingester.IngestAsync(message.CheckId, result, region, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest result for check {CheckId} from worker {ConnectionId}.",
                message.CheckId, Context.ConnectionId);
        }
    }

    /// <summary>Sent by the worker every 60 seconds to signal liveness and report its version.</summary>
    public Task Heartbeat(WorkerHeartbeatMessage message)
    {
        registry.UpdateHeartbeat(Context.ConnectionId, message.Version);
        _ = adminHub.Clients.All.WorkersChanged();
        return Task.CompletedTask;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
