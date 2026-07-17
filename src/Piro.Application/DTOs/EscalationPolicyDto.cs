using System.ComponentModel.DataAnnotations;

namespace Piro.Application.DTOs;

public record EscalationPolicyDto(
    int Id,
    string Name,
    string? Description,
    int ReEscalateAfterInactivityMinutes,
    List<EscalationStepDto> Steps
);

public record EscalationStepDto(
    int Id,
    int Order,
    int DelayMinutes,
    int MaxRetries,
    int RetryIntervalMinutes,
    int ScheduleId,
    string ScheduleName
);

public record UpsertEscalationPolicyRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [StringLength(1000)] string? Description,
    [Range(0, int.MaxValue)] int ReEscalateAfterInactivityMinutes,
    List<UpsertEscalationStepRequest> Steps
);

public record UpsertEscalationStepRequest(
    int Order,
    [Range(0, int.MaxValue)] int DelayMinutes,
    [Range(1, int.MaxValue)] int MaxRetries,
    [Range(0, int.MaxValue)] int RetryIntervalMinutes,
    int ScheduleId
);

/// <summary>A page of <see cref="EscalationPolicyDto"/> results plus the total matching count.</summary>
public record EscalationPolicyPageDto(
    IEnumerable<EscalationPolicyDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
