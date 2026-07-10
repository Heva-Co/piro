using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class EscalationPolicyAppService(IEscalationPolicyRepository repo, IOnCallScheduleRepository scheduleRepo)
{
    public async Task<EscalationPolicyDto?> GetAsync(CancellationToken ct = default)
    {
        var policy = await repo.GetSingleAsync(ct);
        return policy is null ? null : ToDto(policy);
    }

    public async Task<EscalationPolicyDto> UpsertAsync(UpsertEscalationPolicyRequest request, CancellationToken ct = default)
    {
        foreach (var step in request.Steps)
        {
            _ = await scheduleRepo.GetByIdWithLayersAsync(step.ScheduleId, ct)
                ?? throw new NotFoundException(nameof(OnCallSchedule), step.ScheduleId.ToString());
        }

        var policy = new EscalationPolicy
        {
            Name = request.Name,
            Description = request.Description,
            ReEscalateAfterAckMinutes = request.ReEscalateAfterAckMinutes,
            ReEscalateAfterInactivityMinutes = request.ReEscalateAfterInactivityMinutes,
            Steps = request.Steps.Select((s, i) => new EscalationStep
            {
                Order = s.Order,
                DelayMinutes = s.DelayMinutes,
                ScheduleId = s.ScheduleId,
            }).ToList()
        };

        var result = await repo.UpsertAsync(policy, ct);
        return ToDto(result);
    }

    private static EscalationPolicyDto ToDto(EscalationPolicy p) => new(
        p.Id, p.Name, p.Description,
        p.ReEscalateAfterAckMinutes,
        p.ReEscalateAfterInactivityMinutes,
        p.Steps.OrderBy(s => s.Order)
            .Select(s => new EscalationStepDto(s.Id, s.Order, s.DelayMinutes, s.ScheduleId, s.Schedule?.Name ?? ""))
            .ToList()
    );
}
