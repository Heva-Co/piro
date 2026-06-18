using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record NotificationChannelDto(
    int Id,
    string Name,
    NotificationChannelType Type,
    string? Description,
    bool IsInactive,
    string MetaJson,
    bool IsGlobal,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int AlertConfigCount = 0
);

public record CreateNotificationChannelRequest(
    string Name,
    NotificationChannelType Type,
    string? Description = null,
    string MetaJson = "{}",
    bool IsGlobal = false,
    bool IsLocked = false,
    bool IsInactive = false
);

public record UpdateNotificationChannelRequest(
    string? Name,
    string? Description,
    bool? IsInactive,
    string? MetaJson,
    bool? IsGlobal = null,
    bool? IsLocked = null
);
