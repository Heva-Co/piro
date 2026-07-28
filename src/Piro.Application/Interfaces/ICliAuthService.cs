using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>What the browser asks for after the user clicks Authorize.</summary>
/// <param name="RedirectUri">The CLI's loopback callback. Must be 127.0.0.1, ::1 or localhost.</param>
/// <param name="CodeChallenge">Base64url SHA-256 of the CLI's verifier (PKCE S256).</param>
/// <param name="State">The CLI's random state, echoed back on the redirect.</param>
/// <param name="ClientLabel">Free text the CLI reported, e.g. "piro-cli on hostname".</param>
public record CliAuthorizeRequest(
    string RedirectUri,
    string CodeChallenge,
    string State,
    string? ClientLabel);

/// <summary>What the CLI exchanges its code for.</summary>
public record CliTokenRequest(string Code, string CodeVerifier, string RedirectUri);

/// <summary>Issues and redeems the one-time codes behind <c>piro login</c> (RFC 0019 §4.6).</summary>
public interface ICliAuthService
{
    /// <summary>
    /// Mints a single-use code for an already-authenticated user. The raw code is returned once and
    /// only its hash is stored.
    /// </summary>
    Task<string> IssueCodeAsync(AppUser user, CliAuthorizeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Redeems a code, returning the user who authorized it along with the label the CLI reported,
    /// or null if the code is unknown, expired, already used, or fails PKCE or redirect-URI
    /// verification. Marks the code consumed either way, so a wrong verifier cannot be retried
    /// against the same code.
    /// </summary>
    Task<(AppUser User, string? ClientLabel)?> RedeemCodeAsync(
        CliTokenRequest request, CancellationToken ct = default);

    /// <summary>
    /// Whether a callback URL is an acceptable loopback target. Exposed because the consent screen
    /// must reject a bad callback before showing the user anything to approve.
    /// </summary>
    bool IsLoopback(string redirectUri);
}
