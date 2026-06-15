using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// When <c>PIRO_API_WORKER=true</c>, registers the API process itself as a built-in worker
/// so it appears in the Workers UI. Maintains a synthetic heartbeat so it shows as Online.
///
/// This service only affects visibility — the actual execution is handled by
/// <see cref="RemoteCheckJobDispatcher"/> which includes the API in multi-region batches.
/// Non-multi-region checks still run exclusively through <see cref="LocalCheckJobDispatcher"/>
/// to prevent any duplicate execution.
/// </summary>
internal sealed class ApiWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    IWorkerRegistry registry,
    string workerRegion,
    ILogger<ApiWorkerHostedService> logger) : BackgroundService
{
    /// <summary>Fixed well-known ID for the built-in API worker record.</summary>
    public static readonly Guid ApiWorkerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const string ApiWorkerConnectionId = "__api_worker__";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
    private static readonly string ApiVersion = FormatVersion(
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version);

    private static string FormatVersion(System.Version? v) =>
        v is not null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "unknown";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpsertRegistrationAsync(stoppingToken);
        RegisterInRegistry();

        logger.LogInformation(
            "API built-in worker registered (region={Region}, version={Version}).", workerRegion, ApiVersion);

        // Keep heartbeat alive so Workers UI shows the entry as Online
        using var timer = new PeriodicTimer(HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            registry.UpdateHeartbeat(ApiWorkerConnectionId, ApiVersion);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        registry.Unregister(ApiWorkerConnectionId);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PiroDbContext>();
            var existing = await db.WorkerRegistrations.FindAsync([ApiWorkerId], cancellationToken);
            if (existing is not null)
            {
                existing.IsActive = false;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deactivate built-in API worker registration on shutdown.");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task UpsertRegistrationAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PiroDbContext>();

        var existing = await db.WorkerRegistrations.FindAsync([ApiWorkerId], ct);
        if (existing is null)
        {
            db.WorkerRegistrations.Add(new WorkerRegistration
            {
                Id = ApiWorkerId,
                Name = "API (built-in)",
                Region = workerRegion,
                WorkerTokenHash = "builtin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            // Update region in case PIRO_WORKER_REGION changed between restarts
            existing.Region = workerRegion;
            existing.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
    }

    private void RegisterInRegistry()
    {
        var now = DateTime.UtcNow;
        registry.Register(ApiWorkerConnectionId, new WorkerInfo(
            WorkerId: ApiWorkerId,
            ConnectionId: ApiWorkerConnectionId,
            Region: workerRegion,
            ConnectedAt: now,
            LastHeartbeat: now,
            Version: ApiVersion));
    }
}
