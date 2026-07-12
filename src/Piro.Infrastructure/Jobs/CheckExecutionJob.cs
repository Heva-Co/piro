using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Services;
using Quartz;
using Serilog.Context;

namespace Piro.Infrastructure.Jobs;

/// <summary>Quartz job that executes a single check and publishes the result for status recomputation.</summary>
[DisallowConcurrentExecution]
public class CheckExecutionJob(IServiceScopeFactory scopeFactory, ILogger<CheckExecutionJob> logger) : IJob
{
    internal const string CheckIdKey = "checkId";

    public async Task Execute(IJobExecutionContext context)
    {
        if (!context.MergedJobDataMap.TryGetValue(CheckIdKey, out var raw) ||
            !int.TryParse(raw?.ToString(), out var checkId))
        {
            logger.LogWarning("CheckExecutionJob fired without a valid checkId in JobDataMap.");
            return;
        }

        // Tags every log emitted during this execution with CheckId, so the Logs page can filter to a single check.
        using var _ = LogContext.PushProperty("CheckId", checkId);

        // Resolve scoped services — Quartz jobs are singleton-lifetime, so we create a scope manually.
        await using var scope = scopeFactory.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<CheckRunnerService>();

        try
        {
            await runner.RunAsync(checkId, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error executing check {CheckId}.", checkId);
        }
    }
}
