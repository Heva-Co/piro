using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class EscalationPolicyRepository(PiroDbContext db) : IEscalationPolicyRepository
{
    public async Task<EscalationPolicy?> GetSingleAsync(CancellationToken ct = default) =>
        await db.EscalationPolicies
            .Include(p => p.Steps)
                .ThenInclude(s => s.Schedule)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<EscalationPolicy> UpsertAsync(EscalationPolicy policy, CancellationToken ct = default)
    {
        var existing = await db.EscalationPolicies
            .Include(p => p.Steps)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            policy.CreatedAt = DateTime.UtcNow;
            policy.UpdatedAt = DateTime.UtcNow;
            db.EscalationPolicies.Add(policy);
            await db.SaveChangesAsync(ct);
            return await GetSingleAsync(ct) ?? policy;
        }

        existing.Name = policy.Name;
        existing.Description = policy.Description;
        existing.ReEscalateAfterAckMinutes = policy.ReEscalateAfterAckMinutes;
        existing.ReEscalateAfterInactivityMinutes = policy.ReEscalateAfterInactivityMinutes;
        existing.UpdatedAt = DateTime.UtcNow;

        // Replace steps atomically
        db.EscalationSteps.RemoveRange(existing.Steps);
        foreach (var step in policy.Steps)
        {
            step.PolicyId = existing.Id;
            db.EscalationSteps.Add(step);
        }

        await db.SaveChangesAsync(ct);
        return await GetSingleAsync(ct) ?? existing;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var policy = await db.EscalationPolicies.FindAsync([id], ct);
        if (policy is not null)
        {
            db.EscalationPolicies.Remove(policy);
            await db.SaveChangesAsync(ct);
        }
    }
}
