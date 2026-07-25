namespace Piro.Integrations.Abstractions;

/// <summary>
/// The one capability the MobilePush integration needs from Piro that the base host doesn't already
/// provide: reading a user's registered mobile device tokens so a single "MobilePush" personal
/// notification fans out to every device the user owns. Exposed through the host allow-list (RFC 0016
/// §4.2b) — the integration still never touches a repository, the DbContext, or a Piro.Domain type; it
/// asks this narrow reader for a neutral snapshot and does its own delivery.
/// </summary>
public interface IDeviceTokenReader
{
    /// <summary>The mobile device tokens registered for a user, or empty if none.</summary>
    Task<IReadOnlyList<DeviceTokenInfo>> GetByUserIdAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Removes tokens the push provider reported as permanently invalid (unregistered), so a dead
    /// device stops being fanned out to. Called by the dispatcher after a send; a no-op for an empty set.
    /// </summary>
    Task PruneTokensAsync(IEnumerable<string> tokens, CancellationToken ct = default);
}

/// <summary>A neutral snapshot of one registered device, free of any Piro.Domain type.</summary>
public sealed record DeviceTokenInfo(DevicePushPlatform Platform, string Token);

/// <summary>Which push transport a device token targets. Neutral mirror of the domain's platform enum.</summary>
public enum DevicePushPlatform
{
    Android = 0,
    Ios = 1,
}
