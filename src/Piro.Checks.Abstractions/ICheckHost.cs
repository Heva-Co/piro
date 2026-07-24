namespace Piro.Checks.Abstractions;

/// <summary>
/// The narrow, allow-listed window through which a check reaches the rest of Piro — the check analogue
/// of the integration host. A check gets its typed config directly; anything else it needs (an
/// <see cref="HttpClient"/>, or a service an integration registered such as the GCP token provider) it
/// asks the host for. It never sees the <c>Check</c> entity, a repository, or the DbContext.
/// <para>
/// The allow-list is small and explicit; requesting a type outside it throws, rather than silently
/// handing a check a door into Piro's internals.
/// </para>
/// </summary>
public interface ICheckHost
{
    /// <summary>
    /// Resolves a service the check is allowed to use (e.g. <see cref="HttpClient"/>, or the GCP token
    /// provider the GoogleCloud integration registered). Throws when the requested type is not on the
    /// allow-list — that is the boundary doing its job, not a bug.
    /// </summary>
    T GetRequiredService<T>() where T : notnull;
}
