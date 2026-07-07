using Piro.Domain.Enums;

namespace Piro.Application.DTOs;

public record NotificationChannelDto(
    int Id,
    string Name,
    IntegrationType Type,
    string? Description,
    bool IsInactive,
    string MetaJson,
    bool IsGlobal,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int AlertConfigCount = 0,
    int? IntegrationId = null,
    string? IntegrationName = null
);

public record CreateNotificationChannelRequest(
    string Name,
    IntegrationType Type,
    string? Description = null,
    string MetaJson = "{}",
    bool IsGlobal = false,
    bool IsLocked = false,
    bool IsInactive = false,
    int? IntegrationId = null
);

public record UpdateNotificationChannelRequest(
    string? Name,
    string? Description,
    bool? IsInactive,
    string? MetaJson,
    bool? IsGlobal = null,
    bool? IsLocked = null,
    int? IntegrationId = null
);
