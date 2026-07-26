using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Quartz;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// Deletes refresh-token sessions that are revoked or past expiry (RFC 0018), so the RefreshTokens
/// table doesn't grow without bound as devices sign in, rotate, and sign out. Runs daily.
/// </summary>
[DisallowConcurrentExecution]
public class RefreshTokenPruneJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenPruneJob> logger) : IJob
{
    public static readonly JobKey Key = new("refresh-token-prune", "piro");

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var removed = await repo.PruneAsync(DateTime.UtcNow, context.CancellationToken);
        if (removed > 0) logger.LogInformation("Pruned {Count} expired/revoked refresh-token session(s).", removed);
    }
}
