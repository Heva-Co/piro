namespace Piro.Domain.Entities;

/// <summary>
/// A single-use, short-lived code that lets the browser hand a CLI a session it just authorized
/// (RFC 0019 §4.6, §5). The only persistence the config-as-code feature adds.
/// </summary>
/// <remarks>
/// <para>
/// It lives in the database rather than in process memory because the browser and the CLI can reach
/// different API replicas: an in-memory code would work on a single host and fail intermittently
/// behind a load balancer, which is the worst way for an auth flow to break.
/// </para>
/// <para>
/// The shape is PKCE. A loopback listener is reachable by any local process, so possession of the
/// code alone must not be enough to obtain a token — the CLI proves it started the flow by
/// presenting the verifier whose SHA-256 equals <see cref="CodeChallenge"/>. Every field here is
/// load-bearing rather than defense in depth.
/// </para>
/// </remarks>
public class CliAuthorizationCode
{
    public Guid Id { get; set; }

    /// <summary>
    /// SHA-256 (hex) of the raw code. Only the hash is stored, matching how <see cref="ApiKey"/> and
    /// <see cref="RefreshToken"/> handle their secrets, so a database leak yields nothing usable.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// The loopback URL the browser was told to redirect to. Bound at issue time and re-checked at
    /// exchange, so a code minted for one callback cannot be redeemed against another.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Base64url SHA-256 of the CLI's verifier (PKCE S256). Never the verifier itself.</summary>
    public string CodeChallenge { get; set; } = string.Empty;

    /// <summary>
    /// The CLI's random state, echoed back so the CLI can reject a callback it did not initiate.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Shown on the consent screen so the user can tell which machine is asking.</summary>
    public string? ClientLabel { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Expiry in the low minutes — a code is redeemed seconds after it is issued.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Set the first time the code is exchanged. Single use: a second attempt is refused rather than
    /// silently issuing another session.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    public bool IsRedeemable(DateTime now) => ConsumedAt is null && ExpiresAt > now;
}
