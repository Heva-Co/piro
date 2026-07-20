using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// The admin notification-delivery activity feed (RFC 0009 §6, phase 8) — a read-only view over
/// <see cref="Domain.Entities.NotificationDeliveryLog"/> answering "what notifications fired and what
/// happened to each?". Separate from subscription CRUD: a delivery log is its own resource, not a
/// subscription.
/// </summary>
public class DeliveryLogAppService(INotificationDeliveryLogRepository repo)
{
    public async Task<NotificationDeliveryLogPageDto> GetPagedAsync(
        int page, int pageSize, DeliveryStatus? status, CancellationToken ct = default)
    {
        var (items, total) = await repo.GetPagedAsync(page, pageSize, status, ct);
        return new NotificationDeliveryLogPageDto(
            items.Select(l => new NotificationDeliveryLogDto(
                l.Id, l.EventType, l.SubscriptionId, l.TargetKind, l.IntegrationType, l.IntegrationId,
                l.TargetDescriptor, l.Status, l.Error, l.AttemptedAt)),
            total,
            Math.Max(1, page),
            Math.Clamp(pageSize, 10, 200));
    }
}
