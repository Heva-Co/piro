using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Infrastructure.Integrations.OAuth;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// The concrete <see cref="IIntegrationHost"/> (RFC 0016 §4.2b) — the single, narrow window through
/// which an integration's behavior reaches Piro. It hands out only allow-listed services, this
/// integration's own config (read and a bounded write), and OAuth tokens; an integration never sees the
/// container, a repository, or the secret store. This is the root host every integration receives; the
/// UI-registration seam (<see cref="IUIExtensionHost"/>) is one of the allow-listed services it can ask
/// for when it contributes UI.
/// </summary>
internal sealed class IntegrationHost(
    IServiceProvider services,
    IIntegrationRepository integrationRepo,
    IIntegrationRegistry registry,
    ISecretProtector secretProtector,
    IOAuthTokenProvider tokenProvider,
    IOAuthTokenStore tokenStore) : IIntegrationHost
{
    /// <summary>Types an integration is allowed to resolve. Anything else throws (the boundary doing its job).</summary>
    private static readonly HashSet<Type> AllowList =
    [
        typeof(HttpClient),
        typeof(ISecretProtector),
        typeof(ITemplateParser),
        typeof(IUIExtensionHost),
        typeof(IWebhookHost),
        // IAlertService is resolvable here, but the capability gate lives at webhook registration
        // (RegisterWebhook throws at startup unless the integration declares CreatesAlerts), since the
        // shared host has no notion of "which integration is calling" at resolution time.
        typeof(IAlertService),
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

    public Task<string> GetBearerTokenAsync(Guid integrationId, CancellationToken ct = default) =>
        tokenProvider.GetAccessTokenAsync(integrationId, ct);

    public async Task<bool> IsOAuthConnectedAsync(Guid integrationId, CancellationToken ct = default) =>
        await tokenStore.GetAsync(integrationId, ct) is not null;

    public async Task<string?> GetConfigValueAsync(Guid integrationId, string key, CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct);
        if (integration is null || string.IsNullOrWhiteSpace(integration.ConfigJson))
            return null;

        // Read straight from stored ConfigJson: only non-secret keys are read through here, and
        // secret values are stored encrypted, so they'd come back as ciphertext (never usable) anyway.
        using var doc = JsonDocument.Parse(integration.ConfigJson);
        return doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public async Task SetConfigValuesAsync(Guid integrationId, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");

        // Merge non-secret coordinates into the stored ConfigJson without disturbing the encrypted
        // secret fields (they aren't in `values`, so they're untouched). A null value removes the key.
        var config = JsonNode.Parse(
            string.IsNullOrWhiteSpace(integration.ConfigJson) ? "{}" : integration.ConfigJson) as JsonObject
            ?? new JsonObject();
        foreach (var (key, value) in values)
        {
            if (value is null) config.Remove(key);
            else config[key] = value;
        }
        integration.ConfigJson = config.ToJsonString();
        await integrationRepo.UpdateAsync(integration, ct);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
