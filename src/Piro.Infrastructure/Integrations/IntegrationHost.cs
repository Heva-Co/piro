using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// The concrete <see cref="IIntegrationHost"/> (RFC 0016 §4.2b) — the single, narrow window through
/// which an integration's behavior reaches Piro. It hands out only allow-listed services and this
/// integration's own decrypted config; an integration never sees the container, a repository, or the
/// secret store. This is the generalization of RFC 0012's <c>ActionHost</c> from actions to the whole
/// integration surface.
/// </summary>
internal sealed class IntegrationHost(
    IServiceProvider services,
    IIntegrationRepository integrationRepo,
    IIntegrationRegistry registry,
    ISecretProtector secretProtector) : IIntegrationHost
{
    /// <summary>Types an integration is allowed to resolve. Anything else throws (the boundary doing its job).</summary>
    private static readonly HashSet<Type> AllowList =
    [
        typeof(HttpClient),
        typeof(ISecretProtector),
    ];

    public T GetRequiredService<T>() where T : notnull
    {
        if (!AllowList.Contains(typeof(T)))
            throw new InvalidOperationException(
                $"Integration requested {typeof(T).Name}, which is not on the host allow-list. " +
                "Integrations may only resolve allow-listed services (RFC 0016 §4.2b).");

        // HttpClient comes from the shared, pre-configured "piro-webhook" factory client, not a raw new().
        if (typeof(T) == typeof(HttpClient))
            return (T)(object)services.GetRequiredService<IHttpClientFactory>().CreateClient("piro-webhook");

        return services.GetRequiredService<T>();
    }

    public async Task<TConfig?> GetConfigAsync<TConfig>(Guid integrationId, CancellationToken ct = default)
        where TConfig : class
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct);
        if (integration is null || string.IsNullOrWhiteSpace(integration.ConfigJson))
            return null;

        var configType = registry.Find(integration.Type)?.Manifest.ConfigType;
        var decrypted = integration.ReadDecryptedConfigJson(configType, secretProtector);
        return JsonSerializer.Deserialize<TConfig>(decrypted, JsonOpts);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
