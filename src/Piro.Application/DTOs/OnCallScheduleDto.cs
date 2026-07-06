namespace Piro.Application.DTOs;

public record OnCallScheduleDto(
    int Id,
    string Name,
    string? Description,
    string TimeZone,
    bool NotifyOnShiftStart,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OnCallLayerDto> Layers
);

public record OnCallLayerDto(
    int Id,
    int ScheduleId,
    string Name,
    int Order,
    string RecurrenceRule,
    DateTimeOffset FirstOccurrenceStartsAt,
    DateTimeOffset FirstOccurrenceEndsAt,
    bool IsAllDay,
    List<OnCallLayerUserDto> Users
);

public record OnCallLayerUserDto(
    int Id,
    int UserId,
    string UserName,
    string UserInitials,
    string UserColor,
    int Position
);

public record OnCallOverrideDto(
    int Id,
    int ScheduleId,
    int UserId,
    string UserName,
    string UserColor,
    int? ReplacesUserId,
    string? ReplacesUserName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Reason
);

/// <summary>A resolved on-call slot for Gantt rendering.</summary>
public record OnCallSlotDto(
    int LayerId,
    string LayerName,
    int UserId,
    string UserName,
    string UserInitials,
    string UserColor,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsOverride,
    string? ReplacesUserName
);

public record CreateOnCallScheduleRequest(
    string Name,
    string? Description = null,
    string TimeZone = "UTC",
    bool NotifyOnShiftStart = false,
    DateTimeOffset? StartsAtUtc = null,
    DateTimeOffset? EndsAtUtc = null
);

public record UpdateOnCallScheduleRequest(
    string? Name,
    string? Description,
    string? TimeZone,
    bool? NotifyOnShiftStart,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc
);

public record CreateOnCallLayerRequest(
    string Name,
    string RecurrenceRule,
    DateTimeOffset FirstOccurrenceStartsAt,
    DateTimeOffset FirstOccurrenceEndsAt,
    List<int> UserIds
);

public record UpdateOnCallLayerRequest(
    string Name,
    string RecurrenceRule,
    DateTimeOffset FirstOccurrenceStartsAt,
    DateTimeOffset FirstOccurrenceEndsAt,
    List<int> UserIds
);

public record CreateOnCallOverrideRequest(
    int UserId,
    int? ReplacesUserId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Reason = null
);

public record UserNotificationPreferenceDto(
    int Id,
    int IntegrationId,
    string IntegrationName,
    string IntegrationType,
    string Handle,
    int Priority
);

public record SetUserNotificationPreferencesRequest(
    List<UpsertUserNotificationPreferenceRequest> Preferences
);

public record UpsertUserNotificationPreferenceRequest(
    int IntegrationId,
    string Handle,
    int Priority
);
