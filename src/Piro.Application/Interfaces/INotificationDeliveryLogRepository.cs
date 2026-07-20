using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Interfaces;

public interface INotificationDeliveryLogRepository
{
    /// <summary>True if a delivery with this idempotency key already exists — the effectively-once guard.</summary>
    Task<bool> ExistsAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// Records a delivery attempt. Tolerates a concurrent duplicate on the UNIQUE idempotency key by
    /// treating the conflict as "already recorded" rather than throwing.
    /// </summary>
    Task RecordAsync(NotificationDeliveryLog log, CancellationToken ct = default);

    /// <summary>
    /// A page of delivery attempts, newest first, for the admin activity feed. Optionally filtered by
    /// delivery status.
    /// </summary>
    Task<(IEnumerable<NotificationDeliveryLog> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, DeliveryStatus? status, CancellationToken ct = default);
}
