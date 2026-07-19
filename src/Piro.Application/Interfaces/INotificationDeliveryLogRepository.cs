using Piro.Domain.Entities;

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
}
