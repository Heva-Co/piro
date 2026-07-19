using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Attributes;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Infrastructure.Notifications;

/// <summary>
/// The real event processor (RFC 0009 §4.4), replacing the phase-3 no-op. Matches a drained event
/// against enabled subscriptions (by event membership + minimum severity) and delivers each match.
/// <para>
/// Phase 4 scope: <see cref="NotificationTargetKind.Personal"/> destinations are delivered for real via
/// the personal dispatchers wired in phase 1; every attempt is recorded in
/// <see cref="NotificationDeliveryLog"/> keyed by a deterministic idempotency key so a retry can't
/// double-send. Group/Integration destinations are logged as pending until their dispatchers land in
/// phase 5. Only alert events carry a severity today, so only they are matched here.
/// </para>
/// </summary>
internal class SubscriptionMatchingProcessor(
    INotificationSubscriptionRepository subscriptionRepo,
    IUserNotificationPreferenceRepository prefRepo,
    INotificationDeliveryLogRepository deliveryLogRepo,
    IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>> personalDispatchers,
    ILogger<SubscriptionMatchingProcessor> logger) : INotificationEventProcessor
{
    private readonly Dictionary<IntegrationType, IPersonalNotificationDispatcher<AlertNotificationContext>> _personal =
        personalDispatchers.ToDictionary(d => d.Type);

    public async Task ProcessAsync(NotificationEventOutbox outboxRow, CancellationToken ct = default)
    {
        var eventType = NotificationEventTypeExtensions.FromWireName(outboxRow.EventType);
        if (eventType is null)
        {
            logger.LogWarning("Outbox #{Id} has unknown event type {EventType}; skipping.", outboxRow.Id, outboxRow.EventType);
            return;
        }

        // Phase 4 handles alert events (the ones with a severity). Other events drain to Done as no-ops
        // until their handlers land in later phases.
        var alert = TryDeserializeAlert(outboxRow);
        if (alert is null) return;

        var subscriptions = await subscriptionRepo.GetEnabledAsync(ct);
        foreach (var sub in subscriptions)
        {
            if (ct.IsCancellationRequested) break;
            if (!sub.Events().Contains(outboxRow.EventType)) continue;
            if (alert.Severity < sub.MinSeverity) continue;

            await DeliverAsync(sub, outboxRow, alert, ct);
        }
    }

    private async Task DeliverAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, CancellationToken ct)
    {
        // Deterministic per (event × subscription) so a retried outbox row can't double-deliver.
        var idempotencyKey = $"{row.EventType}:{alert.AlertId}:{sub.Id}";
        if (await deliveryLogRepo.ExistsAsync(idempotencyKey, ct)) return;

        if (sub.TargetKind != NotificationTargetKind.Personal)
        {
            // Group/Integration delivery arrives in phase 5. Record the intent so it's visible and
            // the idempotency key is reserved.
            await deliveryLogRepo.RecordAsync(new NotificationDeliveryLog
            {
                IdempotencyKey = idempotencyKey,
                EventType = row.EventType,
                SubscriptionId = sub.Id,
                TargetKind = sub.TargetKind.ToString(),
                TargetDescriptor = sub.Integration?.Name ?? sub.TargetKind.ToString(),
                Status = DeliveryStatus.Skipped,
                Error = "Group/Integration delivery not yet implemented (RFC 0009 phase 5).",
                AttemptedAt = DateTime.UtcNow,
            }, ct);
            return;
        }

        if (sub.UserId is null) return;

        var context = BuildContext(alert);
        var prefs = (await prefRepo.GetByUserIdsAsync([sub.UserId.Value], ct))
            .GetValueOrDefault(sub.UserId.Value, []);

        // First verified, deliverable preference in priority order wins — same rule as escalation.
        foreach (var pref in prefs.OrderBy(p => p.Priority))
        {
            if (!pref.VerifiedAt.HasValue) continue;
            if (pref.Channel.RequiresIntegration() && pref.Integration is null) continue;
            var channelType = pref.Channel.ToIntegrationType();
            if (!_personal.TryGetValue(channelType, out var dispatcher)) continue;

            var descriptor = $"{channelType}:{pref.Handle}";
            try
            {
                var sent = await dispatcher.SendAsync(pref.Integration, pref.Handle, context, ct);
                if (!sent) continue;

                await deliveryLogRepo.RecordAsync(new NotificationDeliveryLog
                {
                    IdempotencyKey = idempotencyKey,
                    EventType = row.EventType,
                    SubscriptionId = sub.Id,
                    TargetKind = sub.TargetKind.ToString(),
                    TargetDescriptor = descriptor,
                    Status = DeliveryStatus.Delivered,
                    AttemptedAt = DateTime.UtcNow,
                }, ct);
                return; // delivered on this preference; done for this subscription
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Notification delivery failed for subscription {Sub} via {Channel}; trying next preference.",
                    sub.Id, channelType);
                // try the next preference; only record a terminal failure if none work (below)
            }
        }

        // No preference delivered — record a single failed row for observability.
        await deliveryLogRepo.RecordAsync(new NotificationDeliveryLog
        {
            IdempotencyKey = idempotencyKey,
            EventType = row.EventType,
            SubscriptionId = sub.Id,
            TargetKind = sub.TargetKind.ToString(),
            TargetDescriptor = $"user:{sub.UserId}",
            Status = DeliveryStatus.Failed,
            Error = "No verified, supported personal preference delivered.",
            AttemptedAt = DateTime.UtcNow,
        }, ct);
    }

    // Minimal snapshot the matcher needs, unified across the three alert payload shapes.
    private sealed record AlertSnapshot(int AlertId, string ServiceName, string CheckName, AlertSeverity Severity, bool IsRecovery);

    private static AlertSnapshot? TryDeserializeAlert(NotificationEventOutbox row)
    {
        switch (row.EventType)
        {
            case "alert:created":
            {
                var p = JsonSerializer.Deserialize<AlertCreatedPayloadV1>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case "alert:acknowledged":
            {
                var p = JsonSerializer.Deserialize<AlertAcknowledgedPayloadV1>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case "alert:resolved":
            {
                var p = JsonSerializer.Deserialize<AlertResolvedPayloadV1>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: true);
            }
            default:
                return null;
        }
    }

    private static AlertNotificationContext BuildContext(AlertSnapshot a) => new(
        ServiceName: a.ServiceName,
        CheckName: a.CheckName,
        CurrentStatus: a.IsRecovery ? ServiceStatus.UP : ServiceStatus.DOWN,
        AlertDescription: null,
        Severity: a.Severity,
        IsRecovery: a.IsRecovery,
        FiredAt: DateTimeOffset.UtcNow,
        AlertId: a.AlertId);
}
