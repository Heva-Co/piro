using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

public class EscalationPolicyAppService(IEscalationPolicyRepository repo, IOnCallScheduleRepository scheduleRepo)
{
    public async Task<EscalationPolicyPageDto> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repo.GetPagedAsync(page, pageSize, ct);
        return new EscalationPolicyPageDto(
            items.Select(p => p.ToDto()),
            total,
            Math.Max(1, page),
            Math.Clamp(pageSize, 10, 200));
    }

    public async Task<EscalationPolicyDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var policy = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(EscalationPolicy), id.ToString());
        return policy.ToDto();
    }

    public async Task<EscalationPolicyDto> CreateAsync(UpsertEscalationPolicyRequest request, CancellationToken ct = default)
    {
        await ValidateSchedulesAsync(request, ct);
        await ValidateUniqueNameAsync(request.Name, excludingId: null, ct);

        var policy = new EscalationPolicy
        {
            Name = request.Name,
            Description = request.Description,
            ReEscalateAfterInactivityMinutes = request.ReEscalateAfterInactivityMinutes,
            Steps = request.Steps.Select(s => new EscalationStep
            {
                Order = s.Order,
                DelayMinutes = s.DelayMinutes,
                ScheduleId = s.ScheduleId,
            }).ToList()
        };

        var created = await repo.CreateAsync(policy, ct);
        return created.ToDto();
    }

    public async Task<EscalationPolicyDto> UpdateAsync(int id, UpsertEscalationPolicyRequest request, CancellationToken ct = default)
    {
        _ = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(EscalationPolicy), id.ToString());
        await ValidateSchedulesAsync(request, ct);
        await ValidateUniqueNameAsync(request.Name, excludingId: id, ct);

        var policy = new EscalationPolicy
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            ReEscalateAfterInactivityMinutes = request.ReEscalateAfterInactivityMinutes,
            Steps = request.Steps.Select(s => new EscalationStep
            {
                Order = s.Order,
                DelayMinutes = s.DelayMinutes,
                ScheduleId = s.ScheduleId,
            }).ToList()
        };

        var updated = await repo.UpdateAsync(policy, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var policy = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(EscalationPolicy), id.ToString());

        if (await repo.IsInUseAsync(id, ct))
            throw new DomainValidationException("This escalation policy is assigned to one or more services. Reassign or unassign them before deleting it.");

        await repo.DeleteAsync(policy, ct);
    }

    private async Task ValidateSchedulesAsync(UpsertEscalationPolicyRequest request, CancellationToken ct)
    {
        foreach (var step in request.Steps)
        {
            _ = await scheduleRepo.GetByIdWithLayersAsync(step.ScheduleId, ct)
                ?? throw new NotFoundException(nameof(OnCallSchedule), step.ScheduleId.ToString());
        }
    }

    private async Task ValidateUniqueNameAsync(string name, int? excludingId, CancellationToken ct)
    {
        if (await repo.ExistsByNameAsync(name, excludingId, ct))
            throw new DomainValidationException($"An escalation policy named \"{name}\" already exists.");
    }
}
