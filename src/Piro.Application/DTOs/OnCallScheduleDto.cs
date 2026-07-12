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
    List<OnCallLayerDto> Layers,
    List<OnCallOverrideDto> Overrides
);

/// <summary>A page of <see cref="OnCallScheduleDto"/> results plus the total matching count.</summary>
public record OnCallSchedulePageDto(
    IEnumerable<OnCallScheduleDto> Items,
    int TotalCount,
    int Page,
    int PageSize
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

/// <summary>Lightweight schedule summary — name plus its unique roster, without rotation/RRULE detail.</summary>
public record OnCallScheduleMembersDto(
    int Id,
    string Name,
    List<OnCallMemberDto> Members
);

public record OnCallMemberDto(
    int UserId,
    string UserName,
    string UserInitials,
    string UserColor
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
    string? ReplacesUserName,
    /// <summary>Set when a slot represents shifts pooled across multiple schedules
    /// (see <c>OnCallService.GetMySlotsAsync</c>) — null when scoped to one already-known schedule.</summary>
    int? ScheduleId = null,
    string? ScheduleName = null,
    /// <summary>The layer's position within its schedule (0 = primary). Used to decide whether
    /// an "on-call now" indicator is worth surfacing — a user parked in a high-order
    /// escalation/management layer is on the roster "just in case" and isn't expected to act.</summary>
    int LayerOrder = 0,
    /// <summary>True if this slot's schedule is the first escalation step (Order 0) of at least
    /// one EscalationPolicy. False marks a schedule used only as a later/backup step in every
    /// policy that references it — set on <c>OnCallService.GetMySlotsAsync</c> results so the
    /// personal calendar can flag "you're backup here, not primary."</summary>
    bool IsPrimarySchedule = true
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

/// <summary>
/// A batch of rotation-layer and override changes for one schedule, applied atomically —
/// all operations commit together or none do. Backs the "Save" button on the schedule detail
/// page, which lets the user stage several layer/override edits before persisting any of them.
/// </summary>
public record SaveRotationsRequest(
    List<CreateOnCallLayerRequest> LayersToCreate,
    List<UpdateExistingLayerRequest> LayersToUpdate,
    List<int> LayerIdsToDelete,
    List<CreateOnCallOverrideRequest> OverridesToCreate,
    List<int> OverrideIdsToDelete
);

public record UpdateExistingLayerRequest(
    int LayerId,
    string Name,
    string RecurrenceRule,
    DateTimeOffset FirstOccurrenceStartsAt,
    DateTimeOffset FirstOccurrenceEndsAt,
    List<int> UserIds
);

/// <summary>
/// Coverage gaps found when previewing a draft's rotation/override changes before saving —
/// windows within the requested range where no layer or override resolves an on-call user.
/// </summary>
public record RotationsPreviewDto(
    List<OnCallSlotDto> Slots,
    List<CoverageGapDto> Gaps
);

public record CoverageGapDto(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt
);

public record UserNotificationPreferenceDto(
    int Id,
    string Channel,
    int? IntegrationId,
    string? IntegrationName,
    string Handle,
    int Priority,
    bool IsVerified,
    bool IsAccountFallback
);

public record UpsertUserNotificationPreferenceRequest(
    string Channel,
    int? IntegrationId,
    string Handle
);

public record ReorderUserNotificationPreferencesRequest(
    List<int> OrderedIds
);

public record ConfirmNotificationPreferenceCodeRequest(string Code);
