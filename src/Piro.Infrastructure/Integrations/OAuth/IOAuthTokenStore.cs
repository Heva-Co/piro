using Piro.Domain.Entities;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Persistent, encrypted-at-rest store for per-integration OAuth token sets. Callers pass and
/// receive plaintext tokens; encryption/decryption (via <c>IDataProtector</c>) happens inside the
/// store so ciphertext never leaks into service code.
/// </summary>
public interface IOAuthTokenStore
{
    /// <summary>Returns the decrypted token set for an integration, or null if none is stored.</summary>
    Task<OAuthTokenSet?> GetAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>Inserts or replaces the token set for an integration, encrypting before persisting.</summary>
    Task SaveAsync(Guid integrationId, OAuthTokenSet tokens, CancellationToken ct = default);

    /// <summary>Removes the stored token set for an integration (e.g. on disconnect).</summary>
    Task DeleteAsync(Guid integrationId, CancellationToken ct = default);
}

/// <summary>A decrypted OAuth token set as seen by service code — never persisted in this shape.</summary>
public sealed record OAuthTokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAt,
    string? Scopes);
