using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IEscalationPolicyRepository
{
    Task<EscalationPolicy?> GetSingleAsync(CancellationToken ct = default);
    Task<EscalationPolicy> UpsertAsync(EscalationPolicy policy, CancellationToken ct = default);
}
