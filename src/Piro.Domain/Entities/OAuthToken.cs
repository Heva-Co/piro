namespace Piro.Domain.Entities;

/// <summary>
/// A persisted OAuth token set for one <see cref="Integration"/>, obtained via the authorization-code
/// flow and kept alive by refresh. Unlike GCP's ephemeral in-memory token cache, this survives
/// restarts, so a "connect once" integration stays connected.
/// <para>
/// <see cref="AccessToken"/> and <see cref="RefreshToken"/> are stored <b>encrypted at rest</b>
/// (via <c>IDataProtector</c>) — they are long-lived credentials that can mint scoped access to the
/// connected third-party account, so they are never persisted or logged in plaintext.
/// </para>
/// </summary>
public class OAuthToken
{
    public Guid Id { get; set; }

    /// <summary>The integration this token belongs to. One token set per integration.</summary>
    public Guid IntegrationId { get; set; }
    public Integration Integration { get; set; } = null!;

    /// <summary>Encrypted access token (ciphertext). Decrypted only in-process when used.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Encrypted refresh token (ciphertext), or null if the provider issued none.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Absolute UTC expiry of the access token (from the provider's <c>expires_in</c>).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Space-separated scopes actually granted.</summary>
    public string? Scopes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
