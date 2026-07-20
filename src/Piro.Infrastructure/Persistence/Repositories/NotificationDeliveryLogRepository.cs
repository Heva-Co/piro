using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class NotificationDeliveryLogRepository(PiroDbContext db) : INotificationDeliveryLogRepository
{
    public Task<bool> ExistsAsync(string idempotencyKey, CancellationToken ct = default) =>
        db.NotificationDeliveryLogs.AnyAsync(l => l.IdempotencyKey == idempotencyKey, ct);

    public async Task RecordAsync(NotificationDeliveryLog log, CancellationToken ct = default)
    {
        db.NotificationDeliveryLogs.Add(log);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent worker recorded the same (event × destination) first — the UNIQUE index
            // rejected this insert. That is the idempotency guarantee working; treat as already done.
            db.Entry(log).State = EntityState.Detached;
        }
    }

    public async Task<(IEnumerable<NotificationDeliveryLog> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, DeliveryStatus? status, CancellationToken ct = default)
    {
        var q = db.NotificationDeliveryLogs.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(l => l.Status == status.Value);

        var total = await q.CountAsync(ct);
        var clampedPageSize = Math.Clamp(pageSize, 10, 200);
        var clampedPage = Math.Max(1, page);

        var items = await q
            .OrderByDescending(l => l.AttemptedAt)
            .ThenByDescending(l => l.Id)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
