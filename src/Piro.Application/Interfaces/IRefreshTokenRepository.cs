using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Store for per-device refresh-token sessions (RFC 0018).</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Finds a session by its token hash, with the owning user loaded. Null if unknown.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    Task UpdateAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Revokes every non-revoked session for a user — used for "sign out everywhere" and on
    /// refresh-token reuse detection.</summary>
    Task RevokeAllForUserAsync(int userId, DateTime now, CancellationToken ct = default);

    /// <summary>Deletes rows that are revoked or past expiry. Returns the number removed.</summary>
    Task<int> PruneAsync(DateTime now, CancellationToken ct = default);
}
