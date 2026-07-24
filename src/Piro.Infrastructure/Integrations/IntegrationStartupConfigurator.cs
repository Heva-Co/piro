using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// Runs each integration's <see cref="IIntegration.Configure"/> once at application startup (RFC 0016).
/// This is where an integration imperatively registers what it contributes — a Jira integration resolves
/// <see cref="IUIExtensionHost"/> from the host and adds its actions/options providers into the singleton
/// registry. Runs only in a live application (a hosted service), never during design-time OpenAPI
/// generation, so the "pure data" contract of reading <see cref="IIntegration.Manifest"/> stays intact.
/// </summary>
internal sealed class IntegrationStartupConfigurator(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var host = scope.ServiceProvider.GetRequiredService<IIntegrationHost>();
        foreach (var integration in scope.ServiceProvider.GetServices<IIntegration>())
        {
            integration.Configure(host);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
