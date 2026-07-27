using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

/// <summary>Payload the mobile app sends to register (or refresh) its push token — see DevicesController.</summary>
public record RegisterDeviceRequest(DevicePlatform Platform, string Token, string? DeviceName, string? PushPublicKey = null);

/// <summary>One of the current user's registered devices.</summary>
public record DeviceDto(int Id, DevicePlatform Platform, string? DeviceName, DateTime LastSeenAt);
