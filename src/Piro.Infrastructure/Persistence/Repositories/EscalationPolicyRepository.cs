using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class EscalationPolicyRepository(PiroDbContext db) : IEscalationPolicyRepository
{
    public async Task<(IEnumerable<EscalationPolicy> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.EscalationPolicies.AsQueryable();

        var total = await q.CountAsync(ct);
        var clampedPageSize = Math.Clamp(pageSize, 10, 200);
        var clampedPage = Math.Max(1, page);

        var items = await q
            .Include(p => p.Steps)
                .ThenInclude(s => s.Schedule)
            .OrderBy(p => p.Name)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<EscalationPolicy?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.EscalationPolicies
            .Include(p => p.Steps)
                .ThenInclude(s => s.Schedule)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<EscalationPolicy> CreateAsync(EscalationPolicy policy, CancellationToken ct = default)
    {
        policy.CreatedAt = DateTime.UtcNow;
        policy.UpdatedAt = DateTime.UtcNow;
        db.EscalationPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(policy.Id, ct) ?? policy;
    }

    public async Task<EscalationPolicy> UpdateAsync(EscalationPolicy policy, CancellationToken ct = default)
    {
        var existing = await db.EscalationPolicies
            .Include(p => p.Steps)
            .FirstAsync(p => p.Id == policy.Id, ct);

        existing.Name = policy.Name;
        existing.Description = policy.Description;
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
        return await GetByIdAsync(existing.Id, ct) ?? existing;
    }

    public async Task<bool> IsInUseAsync(int id, CancellationToken ct = default) =>
        await db.Services.AnyAsync(s => s.EscalationPolicyId == id, ct);

    public async Task<bool> IsScheduleFirstStepInAnyPolicyAsync(int scheduleId, CancellationToken ct = default) =>
        await db.EscalationSteps.AnyAsync(s => s.ScheduleId == scheduleId && s.Order == 0, ct);

    public async Task<bool> ExistsByNameAsync(string name, int? excludingId, CancellationToken ct = default) =>
        await db.EscalationPolicies.AnyAsync(p => p.Name == name && (excludingId == null || p.Id != excludingId), ct);

    public async Task DeleteAsync(EscalationPolicy policy, CancellationToken ct = default)
    {
        db.EscalationPolicies.Remove(policy);
        await db.SaveChangesAsync(ct);
    }
}
