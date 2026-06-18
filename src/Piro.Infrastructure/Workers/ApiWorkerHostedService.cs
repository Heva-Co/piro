using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Workers;

/// <summary>
/// Always registers the API process itself as a built-in worker in the DB so it appears
/// in the Workers UI. Reads <c>worker:builtin_disabled</c> from SiteConfig at startup:
/// when absent/false the worker goes Online and executes checks; when true it stays Offline.
/// To apply a change, set the config value and restart the application.
/// </summary>
internal sealed class ApiWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    IWorkerRegistry registry,
    string workerRegion,
    ILogger<ApiWorkerHostedService> logger) : BackgroundService
{
    /// <summary>Fixed well-known ID for the built-in API worker record.</summary>
    public static readonly Guid ApiWorkerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    internal const string ApiWorkerConnectionId = "__api_worker__";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
    private static readonly string ApiVersion = FormatVersion(
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version);

    private static string FormatVersion(System.Version? v) =>
        v is not null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "unknown";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var disabled = await ReadDisabledFlagAsync(stoppingToken);
        await UpsertRegistrationAsync(stoppingToken);

        if (!disabled)
        {
            RegisterInRegistry();
            logger.LogInformation(
                "API built-in worker online (region={Region}, version={Version}).", workerRegion, ApiVersion);

            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                registry.UpdateHeartbeat(ApiWorkerConnectionId, ApiVersion);
        }
        else
        {
            logger.LogInformation(
                "API built-in worker offline — worker:builtin_disabled=true (region={Region}).", workerRegion);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        registry.Unregister(ApiWorkerConnectionId);
        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> ReadDisabledFlagAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfigRepository>();
            var cfg = await siteConfig.GetAsync(ct);
            return cfg.BuiltinWorkerDisabled;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read worker:builtin_disabled — defaulting to enabled.");
            return false;
        }
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
                IsBuiltIn = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Region = workerRegion;
            existing.IsActive = true;
            existing.IsBuiltIn = true;
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
            Version: ApiVersion,
            IsDefault: true));
    }
}
