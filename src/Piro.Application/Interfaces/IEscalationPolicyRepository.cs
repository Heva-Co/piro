using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IEscalationPolicyRepository
{
    Task<(IEnumerable<EscalationPolicy> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<EscalationPolicy?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<EscalationPolicy> CreateAsync(EscalationPolicy policy, CancellationToken ct = default);
    Task<EscalationPolicy> UpdateAsync(EscalationPolicy policy, CancellationToken ct = default);

    /// <summary>Returns true if any Service currently references this policy.</summary>
    Task<bool> IsInUseAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// True if the given schedule is used as the first step (Order 0) of at least one
    /// escalation policy — used to decide whether a user on that schedule should see the
    /// "you're on-call now" indicator, as opposed to being a late-escalation/backup layer
    /// nobody expects to act on immediately.
    /// </summary>
    Task<bool> IsScheduleFirstStepInAnyPolicyAsync(int scheduleId, CancellationToken ct = default);

    /// <summary>Returns true if another policy (excluding <paramref name="excludingId"/>) already has this name.</summary>
    Task<bool> ExistsByNameAsync(string name, int? excludingId, CancellationToken ct = default);

    Task DeleteAsync(EscalationPolicy policy, CancellationToken ct = default);
}
