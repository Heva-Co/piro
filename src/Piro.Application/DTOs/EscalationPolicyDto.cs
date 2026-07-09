namespace Piro.Application.DTOs;

public record EscalationPolicyDto(
    int Id,
    string Name,
    string? Description,
    int ReEscalateAfterAckMinutes,
    int ReEscalateAfterInactivityMinutes,
    List<EscalationStepDto> Steps
);

public record EscalationStepDto(
    int Id,
    int Order,
    int DelayMinutes,
    int ScheduleId,
    string ScheduleName
);

public record UpsertEscalationPolicyRequest(
    string Name,
    string? Description,
    int ReEscalateAfterAckMinutes,
    int ReEscalateAfterInactivityMinutes,
    List<UpsertEscalationStepRequest> Steps
);

public record UpsertEscalationStepRequest(
    int Order,
    int DelayMinutes,
    int ScheduleId
);
