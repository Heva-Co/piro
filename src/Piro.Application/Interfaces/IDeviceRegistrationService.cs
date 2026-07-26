using Piro.Application.DTOs;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

/// <summary>
/// Registers and unregisters the current user's mobile devices for on-call push. Registering a device
/// also provisions (once) the user's MobilePush notification preference so the escalation engine will
/// page it — the authenticated registration itself stands in for handle verification, so the
/// preference is created already verified (no OTP round-trip, unlike SMS/Telegram).
/// </summary>
public interface IDeviceRegistrationService
{
    /// <summary>Registers or refreshes a device token for the user and ensures their MobilePush preference exists.</summary>
    Task<DeviceDto> RegisterAsync(int userId, DevicePlatform platform, string token, string? deviceName, CancellationToken ct = default);

    /// <summary>Removes a device token (device sign-out). No-op if it isn't registered.</summary>
    Task UnregisterAsync(int userId, string token, CancellationToken ct = default);

    /// <summary>Lists the user's currently registered devices.</summary>
    Task<List<DeviceDto>> GetDevicesAsync(int userId, CancellationToken ct = default);
}
