using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Generates and validates JWT access tokens and per-device refresh tokens (RFC 0018).</summary>
public interface ITokenService
{
    Task<(string token, DateTime expires)> GenerateAccessTokenAsync(AppUser user);

    /// <summary>Issues a new refresh-token session for the user (one row per device) and returns the raw
    /// token. Does not touch the user's other sessions.</summary>
    Task<string> GenerateRefreshTokenAsync(AppUser user, string? deviceLabel = null, CancellationToken ct = default);

    /// <summary>Validates and rotates a refresh token: if it maps to an active session, revokes that
    /// session and returns the owning user; the caller then issues a fresh token. Returns null if the
    /// token is unknown or expired. On reuse of an already-revoked token, revokes the user's whole chain
    /// (reuse defense) and returns null.</summary>
    Task<AppUser?> RotateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes the single session identified by this refresh token (per-device sign-out). No-op
    /// if unknown.</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes every session for a user ("sign out everywhere").</summary>
    Task RevokeAllAsync(int userId, CancellationToken ct = default);
}
