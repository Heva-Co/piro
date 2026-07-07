using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Services;
using Quartz;

namespace Piro.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class EscalationCheckJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EscalationCheckJob> logger) : IJob
{
    public static readonly JobKey Key = new("escalation-check", "piro");

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var checker = scope.ServiceProvider.GetRequiredService<EscalationCheckerService>();
        await checker.ProcessAsync(context.CancellationToken);
        logger.LogDebug("Escalation check completed.");
    }
}
