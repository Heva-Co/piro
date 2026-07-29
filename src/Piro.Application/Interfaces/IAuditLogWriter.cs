using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Records audit entries for things the interceptor cannot see (issue #17).
/// </summary>
/// <remarks>
/// The interceptor only observes changes to <see cref="Piro.Domain.Auditing.IAuditable"/> entities
/// as they pass through <c>SaveChanges</c>. Authentication is not a change to an entity — a
/// successful login writes no audited row at all, and a rejected one writes nothing whatsoever — so
/// those events have to be stated explicitly.
/// <para>
/// A failed login is also the one case where an entry is written with no authenticated user, since
/// the whole point is that authentication did not succeed. The identity recorded is therefore the
/// attempted one, which is a claim about the request rather than a verified actor.
/// </para>
/// </remarks>
public interface IAuditLogWriter
{
    /// <summary>
    /// Records an authentication event. <paramref name="userId"/> is empty when the attempt failed
    /// before resolving a user.
    /// </summary>
    Task WriteAuthEventAsync(
        AuditAction action,
        string userId,
        string email,
        string? ipAddress,
        CancellationToken ct = default);
}
