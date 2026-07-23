namespace Piro.Integrations.Abstractions;

/// <summary>
/// The narrow, allow-listed window through which an integration's behavior reaches the rest of Piro
/// (RFC 0016 §4.2b). "Integrations know nothing": an integration never sees the DI container, a
/// repository, the DbContext, or an ambient HttpClient. It asks the host for a service it is
/// <b>allowed</b> to use, and the host decides what it may have.
/// <para>
/// This generalizes RFC 0012's <c>IActionHost</c> ("the internal SDK a plugin consumes, reaching
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
}
