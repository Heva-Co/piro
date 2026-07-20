using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Notifications;

/// <summary>
/// Writes an emitted event to the outbox as a single Pending row (RFC 0009 §4.6). Serializes by the
/// event's concrete runtime type so the full contracted payload is persisted; the auto-increment
/// <see cref="NotificationEventOutbox.Id"/> assigned on save is the event's ordering sequence number.
/// </summary>
internal class NotificationEventPublisher(PiroDbContext db, ILogger<NotificationEventPublisher> logger) : INotificationEventPublisher
{
    public async Task<long> PublishAsync(INotificationEvent evt, string orderingKey, CancellationToken ct = default)
    {
        var row = new NotificationEventOutbox
        {
            EventType = evt.EventType,
            OrderingKey = orderingKey,
            // Serialize against the concrete type so every payload field is captured, not just the
            // INotificationEvent surface.
            PayloadJson = JsonSerializer.Serialize(evt, evt.GetType()),
            Status = OutboxStatus.Pending,
            Attempts = 0,
            NextAttemptAt = null,
            // CreatedAt is stamped by PiroDbContext's audit hook on save.
        };

        db.NotificationEventOutbox.Add(row);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[notify] event {EventType} published to outbox #{Id} (ordering {OrderingKey}).",
            evt.EventType, row.Id, orderingKey);
        return row.Id;
    }
}
