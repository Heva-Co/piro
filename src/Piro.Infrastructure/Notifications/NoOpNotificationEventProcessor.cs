using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Notifications;

/// <summary>
/// Phase-3 placeholder processor (RFC 0009 §4.6): the transport (outbox + worker) is complete and
/// fully exercised, but no source publishes events yet and subscription matching arrives in later
/// phases. This impl accepts every drained row and does nothing, so the worker marks it Done — proving
/// the drain/order/retry machinery end to end. It is replaced by the real subscription-matching
/// processor when phases 4–5 land; the <see cref="INotificationEventProcessor"/> contract stays stable.
/// </summary>
internal class NoOpNotificationEventProcessor(ILogger<NoOpNotificationEventProcessor> logger)
    : INotificationEventProcessor
{
    public Task ProcessAsync(NotificationEventOutbox outboxRow, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Notification event {EventType} (outbox #{Id}, ordering {OrderingKey}) drained; no processor wired yet (RFC 0009 phase 3).",
            outboxRow.EventType, outboxRow.Id, outboxRow.OrderingKey);
        return Task.CompletedTask;
    }
}
