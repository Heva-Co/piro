using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

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
}
