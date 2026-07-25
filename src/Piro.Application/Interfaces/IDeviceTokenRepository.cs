using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

public interface IDeviceTokenRepository
{
    /// <summary>All registered device tokens for a user — the fan-out target when the user is paged.</summary>
    Task<List<DeviceToken>> GetByUserIdAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Registers a device token for a user, or refreshes the existing row if that (user, token) pair is
    /// already registered (updating platform/name/LastSeenAt and clearing the failure count). Idempotent.
    /// </summary>
    Task<DeviceToken> UpsertAsync(int userId, DevicePlatform platform, string token, string? deviceName, CancellationToken ct = default);

    /// <summary>Removes a device token for a user (device sign-out). No-op if it doesn't exist.</summary>
    Task DeleteByTokenAsync(int userId, string token, CancellationToken ct = default);

    /// <summary>Removes a set of tokens the push provider reported as permanently invalid (unregistered).</summary>
    Task PruneTokensAsync(IEnumerable<string> tokens, CancellationToken ct = default);

    /// <summary>Records a transient delivery failure against a token, incrementing its failure count.</summary>
    Task IncrementFailureAsync(string token, CancellationToken ct = default);
}
