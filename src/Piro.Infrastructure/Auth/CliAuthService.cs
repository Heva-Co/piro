using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Auth;

/// <summary>
/// Issues and redeems the one-time codes behind <c>piro login</c> (RFC 0019 §4.6).
/// </summary>
/// <remarks>
/// A loopback listener is reachable by every local process, so the code alone must never be enough:
/// PKCE, single use, a short TTL, and redirect-URI binding are all load-bearing here rather than
/// defense in depth. This is a small amount of code that warrants disproportionate scrutiny, which
/// is why it is isolated in its own phase.
/// </remarks>
internal sealed class CliAuthService(
    PiroDbContext db,
    TimeProvider clock,
    ILogger<CliAuthService> logger) : ICliAuthService
{
    /// <summary>A code is redeemed seconds after it is issued; minutes is already generous.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<string> IssueCodeAsync(
        AppUser user, CliAuthorizeRequest request, CancellationToken ct = default)
    {
        // Re-checked here and not only in the UI: the consent screen is a client, and a client's
        // validation is a courtesy, not a control. Without this, a crafted link could forward a
        // token to an attacker's origin.
        if (!IsLoopback(request.RedirectUri))
            throw new ArgumentException("The CLI callback must be a loopback address.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.CodeChallenge) || string.IsNullOrWhiteSpace(request.State))
            throw new ArgumentException("A code challenge and state are required.", nameof(request));

        var now = clock.GetUtcNow().UtcDateTime;

        // Opportunistic sweep: expired rows are worthless and this keeps the table from growing
        // without a background job for one small table.
        await db.CliAuthorizationCodes
            .Where(c => c.ExpiresAt < now.AddHours(-1))
            .ExecuteDeleteAsync(ct);

        var rawCode = GenerateCode();

        db.CliAuthorizationCodes.Add(new CliAuthorizationCode
        {
            CodeHash = Hash(rawCode),
            UserId = user.Id,
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            State = request.State,
            ClientLabel = Truncate(request.ClientLabel, 200),
            ExpiresAt = now.Add(Lifetime),
        });

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Issued a CLI authorization code for user {UserId}.", user.Id);
        return rawCode;
    }

    public async Task<(AppUser User, string? ClientLabel)?> RedeemCodeAsync(
        CliTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.CodeVerifier))
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(request.Code);

        var code = await db.CliAuthorizationCodes
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CodeHash == hash, ct);

        if (code is null)
        {
            logger.LogWarning("A CLI token exchange presented an unknown code.");
            return null;
        }

        // Consume before validating anything else. A code that has been presented is spent, so a
        // wrong verifier cannot be retried against it — otherwise a local process that stole the
        // code could brute-force the verifier.
        var redeemable = code.IsRedeemable(now);
        code.ConsumedAt ??= now;
        await db.SaveChangesAsync(ct);

        if (!redeemable)
        {
            logger.LogWarning(
                "A CLI token exchange presented an expired or already-used code for user {UserId}.",
                code.UserId);
            return null;
        }

        // The code was minted for one callback; redeeming it against another means something other
        // than the CLI that started the flow is asking.
        if (!string.Equals(code.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
        {
            logger.LogWarning("A CLI token exchange used a redirect URI the code was not issued for.");
            return null;
        }

        if (!VerifyChallenge(request.CodeVerifier, code.CodeChallenge))
        {
            logger.LogWarning("A CLI token exchange failed PKCE verification for user {UserId}.", code.UserId);
            return null;
        }

        return (code.User, code.ClientLabel);
    }

    /// <summary>
    /// Accepts only a loopback host over http, plus https for completeness. Anything else — a public
    /// hostname, a custom scheme, an IP that merely looks local — is refused, because this is the
    /// single control standing between a crafted consent link and a token sent to an attacker.
    /// </summary>
    public bool IsLoopback(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        return uri.Host is "127.0.0.1" or "::1" or "[::1]" or "localhost";
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateCode() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>
    /// PKCE S256: the challenge is the base64url SHA-256 of the verifier. Compared in fixed time so
    /// the check cannot be turned into an oracle.
    /// </summary>
    private static bool VerifyChallenge(string verifier, string challenge)
    {
        var computed = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed), Encoding.UTF8.GetBytes(challenge));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
