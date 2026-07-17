using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class EscalationPolicyExtensions
{
    /// <summary>Maps an <see cref="EscalationPolicy"/> entity to its outbound DTO representation.</summary>
    public static EscalationPolicyDto ToDto(this EscalationPolicy p) => new(
        p.Id, p.Name, p.Description,
        p.ReEscalateAfterInactivityMinutes,
        p.Steps.OrderBy(s => s.Order)
            .Select(s => new EscalationStepDto(s.Id, s.Order, s.DelayMinutes, s.MaxRetries, s.RetryIntervalMinutes, s.ScheduleId, s.Schedule?.Name ?? ""))
            .ToList()
    );
}
