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
    /// Resolves a service the integration is allowed to use (e.g. <c>HttpClient</c>). Throws when the
    /// requested type is not on the allow-list — that is the boundary doing its job, not a bug. An
    /// integration that needs HTTP requests one here rather than constructing its own; one that needs
    /// nothing asks for nothing.
    /// </summary>
    T GetRequiredService<T>() where T : notnull;
}
