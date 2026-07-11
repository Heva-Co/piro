using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class EscalationPolicyAppService(IEscalationPolicyRepository repo, IOnCallScheduleRepository scheduleRepo)
{
    public async Task<EscalationPolicyDto?> GetAsync(CancellationToken ct = default)
    {
        var policy = await repo.GetSingleAsync(ct);
        return policy?.ToDto();
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
        return result.ToDto();
    }
}
