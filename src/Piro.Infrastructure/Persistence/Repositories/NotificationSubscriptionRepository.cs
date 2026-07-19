using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class NotificationSubscriptionRepository(PiroDbContext db) : INotificationSubscriptionRepository
{
    public async Task<(IEnumerable<NotificationSubscription> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.NotificationSubscriptions.AsQueryable();

        var total = await q.CountAsync(ct);
        var clampedPageSize = Math.Clamp(pageSize, 10, 200);
        var clampedPage = Math.Max(1, page);

        var items = await q
            .Include(s => s.User)
            .Include(s => s.Integration)
            .OrderBy(s => s.Name)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<NotificationSubscription?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.NotificationSubscriptions
            .Include(s => s.User)
            .Include(s => s.Integration)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<NotificationSubscription> CreateAsync(NotificationSubscription subscription, CancellationToken ct = default)
    {
        subscription.CreatedAt = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;
        db.NotificationSubscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(subscription.Id, ct) ?? subscription;
    }

    public async Task<NotificationSubscription> UpdateAsync(NotificationSubscription subscription, CancellationToken ct = default)
    {
        var existing = await db.NotificationSubscriptions.FirstAsync(s => s.Id == subscription.Id, ct);

        existing.Name = subscription.Name;
        existing.EventsJson = subscription.EventsJson;
        existing.MinSeverity = subscription.MinSeverity;
        existing.TargetKind = subscription.TargetKind;
        existing.UserId = subscription.UserId;
        existing.IntegrationId = subscription.IntegrationId;
        existing.Target = subscription.Target;
        existing.Enabled = subscription.Enabled;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(existing.Id, ct) ?? existing;
    }

    public async Task DeleteAsync(NotificationSubscription subscription, CancellationToken ct = default)
    {
        db.NotificationSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationSubscription>> GetEnabledAsync(CancellationToken ct = default) =>
        await db.NotificationSubscriptions
            .Include(s => s.User)
            .Include(s => s.Integration)
            .Where(s => s.Enabled)
            .ToListAsync(ct);
}
