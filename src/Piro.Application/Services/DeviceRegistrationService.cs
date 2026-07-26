using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Default <see cref="IDeviceRegistrationService"/>. Registering a device is idempotent (upsert on the
/// (user, token) pair) and, on a user's first device, provisions their single MobilePush notification
/// preference: it points at the platform-wide MobilePush integration instance and carries the user's id
/// as its handle, so the one preference fans out to every device the user owns. The preference is
/// created already verified — the authenticated registration is the proof of ownership.
/// </summary>
public class DeviceRegistrationService(
    IDeviceTokenRepository deviceRepo,
    IIntegrationRepository integrationRepo,
    IUserNotificationPreferenceRepository prefRepo) : IDeviceRegistrationService
{
    private const string MobilePushType = "MobilePush";

    public async Task<DeviceDto> RegisterAsync(int userId, DevicePlatform platform, string token, string? deviceName, CancellationToken ct = default)
    {
        var device = await deviceRepo.UpsertAsync(userId, platform, token, deviceName, ct);
        await EnsureMobilePushPreferenceAsync(userId, ct);
        return Map(device);
    }

    public async Task UnregisterAsync(int userId, string token, CancellationToken ct = default)
    {
        await deviceRepo.DeleteByTokenAsync(userId, token, ct);
    }

    public async Task<List<DeviceDto>> GetDevicesAsync(int userId, CancellationToken ct = default)
    {
        var devices = await deviceRepo.GetByUserIdAsync(userId, ct);
        return devices.Select(Map).ToList();
    }

    /// <summary>
    /// Ensures the user has exactly one MobilePush preference, verified, pointing at the platform-wide
    /// MobilePush integration instance (created on demand). Idempotent — subsequent registrations no-op.
    /// </summary>
    private async Task EnsureMobilePushPreferenceAsync(int userId, CancellationToken ct)
    {
        var integration = await GetOrCreateMobilePushIntegrationAsync(ct);

        var prefs = await prefRepo.GetByUserIdAsync(userId, ct);
        var existing = prefs.FirstOrDefault(p => p.IntegrationInstanceId == integration.Id);
        if (existing is not null)
        {
            // Re-affirm verification in case it was cleared (e.g. handle edited); keep the handle correct.
            if (existing.VerifiedAt is null || existing.Handle != userId.ToString())
            {
                existing.Handle = userId.ToString();
                existing.VerifiedAt = DateTimeOffset.UtcNow;
                await prefRepo.UpdateAsync(existing, ct);
            }
            return;
        }

        var priority = prefs.Count > 0 ? prefs.Max(p => p.Priority) + 1 : 0;
        await prefRepo.CreateAsync(new UserNotificationPreference
        {
            UserId = userId,
            IntegrationInstanceId = integration.Id,
            Handle = userId.ToString(),
            Priority = priority,
            VerifiedAt = DateTimeOffset.UtcNow,
        }, ct);
    }

    private async Task<Integration> GetOrCreateMobilePushIntegrationAsync(CancellationToken ct)
    {
        var all = await integrationRepo.GetAllAsync(ct);
        var existing = all.FirstOrDefault(i => i.Type == MobilePushType);
        if (existing is not null)
            return existing;

        // Platform-wide singleton: one MobilePush instance carries the FCM/APNs credentials for the
        // whole deployment. An admin fills in its config; here we just ensure the row exists so devices
        // can register before that. ConfigJson stays "{}" until configured.
        return await integrationRepo.CreateAsync(new Integration
        {
            Name = "Mobile push",
            Type = MobilePushType,
            Description = "On-call push notifications to the Piro mobile app.",
        }, ct);
    }

    private static DeviceDto Map(DeviceToken d) => new(d.Id, d.Platform, d.DeviceName, d.LastSeenAt);
}
