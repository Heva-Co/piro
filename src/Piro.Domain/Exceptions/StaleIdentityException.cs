namespace Piro.Domain.Exceptions;

/// <summary>
/// Thrown when a request's JWT is well-formed but the account it identifies no longer exists
/// (e.g. deleted after the token was issued). Distinct from <see cref="NotFoundException"/>,
/// which is for looking up some other resource — this means the caller's own session is stale
/// and should be treated as unauthenticated (401), not "resource not found" (404).
/// </summary>
public class StaleIdentityException(int userId) : Exception($"User {userId} no longer exists.")
{
    public int UserId { get; } = userId;
}
