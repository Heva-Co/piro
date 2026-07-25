using Piro.Application.Interfaces;
using Piro.Domain.Enums;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// Bridges the MobilePush integration to the device-token store through the host allow-list: it reads
/// a user's registered devices via <see cref="IDeviceTokenRepository"/> and hands them back as neutral
/// <see cref="DeviceTokenInfo"/> snapshots, so the integration never sees a Piro.Domain type.
/// </summary>
internal sealed class DeviceTokenReader(IDeviceTokenRepository repo) : IDeviceTokenReader
{
    public async Task<IReadOnlyList<DeviceTokenInfo>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        var devices = await repo.GetByUserIdAsync(userId, ct);
        return devices
            .Select(d => new DeviceTokenInfo(MapPlatform(d.Platform), d.Token))
            .ToList();
    }

    public async Task PruneTokensAsync(IEnumerable<string> tokens, CancellationToken ct = default)
    {
        await repo.PruneTokensAsync(tokens, ct);
    }

    private static DevicePushPlatform MapPlatform(DevicePlatform platform) => platform switch
    {
        DevicePlatform.Android => DevicePushPlatform.Android,
        DevicePlatform.Ios => DevicePushPlatform.Ios,
        _ => DevicePushPlatform.Android,
    };
}
