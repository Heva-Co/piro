using Piro.Contracts;
namespace Piro.Integrations.Abstractions;

/// <summary>
/// The narrow, allow-listed window through which an integration's behavior reaches the rest of Piro
/// (RFC 0016 §4.2b). "Integrations know nothing": an integration never sees the DI container, a
/// repository, the DbContext, or an ambient HttpClient. It asks the host for a service it is
/// <b>allowed</b> to use, and the host decides what it may have.
/// <para>
/// This generalizes RFC 0012's <c>IUIExtensionHost</c> ("the internal SDK a plugin consumes, reaching
/// Piro through a bounded surface, not its persistence") from actions-only to the whole integration
/// surface. The allow-list is small and explicit (an <c>HttpClient</c>, a logger, the OAuth
/// bearer-token provider, this integration's own config accessor, the outbound external-reference
/// writer); requesting anything outside it throws, rather than silently handing an integration a
/// door into Piro's internals.
/// </para>
/// </summary>
public interface IIntegrationHost
{
    /// <summary>
    /// Resolves a service the integration is allowed to use (e.g. <see cref="HttpClient"/>,
    /// <see cref="Piro.Contracts.ISecretProtector"/>). Throws when the requested type is not on the
    /// allow-list — that is the boundary doing its job, not a bug. An integration that needs HTTP
    /// requests one here rather than constructing its own; one that needs nothing asks for nothing.
    /// </summary>
    T GetRequiredService<T>() where T : notnull;

    /// <summary>
    /// Returns this integration instance's configuration, deserialized into <typeparamref name="TConfig"/>
    /// with its secret fields already decrypted. The integration reads its own settings without ever
    /// touching the <c>Integration</c> entity, the repository, or the secret store directly — the host
    /// resolves and decrypts on its behalf. Returns null if the integration instance is not found.
    /// </summary>
    Task<TConfig?> GetConfigAsync<TConfig>(Guid integrationId, CancellationToken ct = default) where TConfig : class;

    /// <summary>
    /// Returns a currently-valid OAuth access token for the integration, refreshing transparently
    /// (wraps RFC 0004's token provider). Throws if the integration is not OAuth-connected. Any
    /// integration behavior (dispatcher, discovery, UI extension) that calls an OAuth-authenticated
    /// API gets its bearer token here rather than touching the token store.
    /// </summary>
    Task<string> GetBearerTokenAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// True if the integration currently has a live OAuth connection (a stored, resolvable token).
    /// Lets an integration gate readiness without catching exceptions from <see cref="GetBearerTokenAsync"/>.
    /// </summary>
    Task<bool> IsOAuthConnectedAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// Reads a non-secret value from the integration's ConfigJson by key (e.g. "cloudId", "siteUrl",
    /// "defaultProjectKey"), or null if absent. Secret fields are never returned in plaintext through
    /// this — an integration authenticates via <see cref="GetBearerTokenAsync"/>, not raw credentials.
    /// For a fully-typed, decrypted view prefer <see cref="GetConfigAsync{TConfig}"/>; this is the
    /// single-key convenience.
    /// </summary>
    Task<string?> GetConfigValueAsync(Guid integrationId, string key, CancellationToken ct = default);

    /// <summary>
    /// Writes non-secret coordinates back onto the integration's own ConfigJson (e.g. an OAuth
    /// discovery step persisting its "cloudId"/"siteUrl"), merged without disturbing existing keys or
    /// the encrypted secret fields. This is the bounded write seam: an integration persists its own
    /// derived config through the host, never touching the repository or DbContext (RFC 0016 §4.2b).
    /// Keys mapped to null are removed.
    /// </summary>
    Task SetConfigValuesAsync(Guid integrationId, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default);
}
