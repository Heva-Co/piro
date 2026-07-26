namespace Piro.Domain.Entities;

/// <summary>
/// One active refresh-token session for a user — a single device/login (RFC 0018). A user has many
/// rows, one per device, so signing in on a second device no longer evicts the first. Only the SHA-256
/// <see cref="TokenHash"/> is stored (never the raw token); lookup is by that hashed, indexed value.
/// Rotation on refresh revokes the presented row and inserts a fresh one; sign-out revokes the row(s)
/// for that device.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>SHA-256 (hex) of the raw refresh token. Unique — the raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Optional human label for a future "your sessions" screen (e.g. the device name).</summary>
    public string? DeviceLabel { get; set; }

    /// <summary>Set automatically by the DbContext audit hook (UTC). Kept as DateTime to match every other
    /// entity's CreatedAt so <c>SetAuditTimestamps</c> can stamp it.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Absolute expiry (UTC); a token past this is invalid even if never used.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set (UTC) when the token is rotated or signed out. A non-null value means the row is dead.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
