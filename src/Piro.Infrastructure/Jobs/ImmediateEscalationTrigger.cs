using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Services;

namespace Piro.Infrastructure.Jobs;

/// <summary>
/// Fire-and-forget immediate escalation. Opens its own DI scope (like the Quartz job) so it never
/// shares the creating request's DbContext, and runs off the request thread so alert creation isn't
/// blocked by notification I/O. The every-minute EscalationCheckJob still owns retries/handoff — this
/// only removes the up-to-60s wait before the first page.
/// </summary>
public class ImmediateEscalationTrigger(
    IServiceScopeFactory scopeFactory,
    ILogger<ImmediateEscalationTrigger> logger) : IImmediateEscalationTrigger
{
    public void Trigger(int alertId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var checker = scope.ServiceProvider.GetRequiredService<EscalationCheckerService>();
                await checker.ProcessOneAsync(alertId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Immediate escalation trigger failed for alert #{AlertId}.", alertId);
            }
        });
    }
}
