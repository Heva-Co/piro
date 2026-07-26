using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Auth;

/// <summary>
/// Generates JWT access tokens and manages per-device refresh-token sessions (RFC 0018). Refresh tokens
/// are opaque random values; only their SHA-256 hash is stored, and each device gets its own
/// <see cref="RefreshToken"/> row so signing in on one device never evicts another.
/// </summary>
public class TokenService(
    IConfiguration config,
    UserManager<AppUser> userManager,
    IRefreshTokenRepository refreshTokens) : ITokenService
{
    private readonly string _secret = config["Auth:JwtSecret"]
        ?? throw new InvalidOperationException("Auth:JwtSecret is required.");
    private readonly int _accessExpiryMinutes = int.TryParse(config["Auth:AccessTokenExpiryMinutes"], out var v) ? v : 60;
    private readonly int _refreshExpiryDays = int.TryParse(config["Auth:RefreshTokenExpiryDays"], out var d) ? d : 30;

    /// <summary>Creates a signed JWT for the given user, including their roles as claims.</summary>
    public async Task<(string token, DateTime expires)> GenerateAccessTokenAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("name", user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_accessExpiryMinutes);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>Issues a new refresh-token session (one row per device); returns the raw token.</summary>
    public async Task<string> GenerateRefreshTokenAsync(AppUser user, string? deviceLabel = null, CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        // CreatedAt is stamped by the DbContext audit hook; we only set the expiry.
        await refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(raw),
            DeviceLabel = deviceLabel,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshExpiryDays),
        }, ct);
        return raw;
    }

    /// <summary>Validates + rotates: revokes the presented session and returns its user, or null if the
    /// token is unknown/expired. Reuse of an already-revoked token revokes the user's whole chain.</summary>
    public async Task<AppUser?> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = await refreshTokens.GetByHashAsync(Hash(refreshToken), ct);
        if (session is null) return null;

        // Replay of a revoked token → treat every session for this user as compromised.
        if (session.RevokedAt is not null)
        {
            await refreshTokens.RevokeAllForUserAsync(session.UserId, now, ct);
            return null;
        }

        if (session.ExpiresAt <= now) return null;
        if (!session.User.IsActive) return null;

        session.RevokedAt = now; // rotation: this token is spent; caller issues a fresh one
        await refreshTokens.UpdateAsync(session, ct);
        return session.User;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var session = await refreshTokens.GetByHashAsync(Hash(refreshToken), ct);
        if (session is null || session.RevokedAt is not null) return;
        session.RevokedAt = DateTime.UtcNow;
        await refreshTokens.UpdateAsync(session, ct);
    }

    public Task RevokeAllAsync(int userId, CancellationToken ct = default) =>
        refreshTokens.RevokeAllForUserAsync(userId, DateTime.UtcNow, ct);

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
