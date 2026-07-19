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
/// The real event processor (RFC 0009 §4.4). Matches a drained event
/// against enabled subscriptions (by event membership + minimum severity) and delivers each match to
/// its destination — Personal (a person's channels), Channel (a team channel, e.g. Google Chat), or
/// Integration (an external platform, e.g. PagerDuty, whose routing key is resolved per-service from
/// the RFC-0004 <see cref="ServiceIntegrationMapping"/>). Every attempt is recorded in
/// <see cref="NotificationDeliveryLog"/> keyed by a deterministic idempotency key so a retry can't
/// double-send. Only alert events carry a severity today, so only they are matched here.
/// </summary>
internal class SubscriptionMatchingProcessor(
    INotificationSubscriptionRepository subscriptionRepo,
    IUserNotificationPreferenceRepository prefRepo,
    INotificationDeliveryLogRepository deliveryLogRepo,
    IServiceIntegrationMappingRepository mappingRepo,
    IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>> personalDispatchers,
    IEnumerable<IChannelNotificationDispatcher<AlertNotificationContext>> channelDispatchers,
    IEnumerable<ISystemEventDispatcher> systemEventDispatchers,
    ILogger<SubscriptionMatchingProcessor> logger) : INotificationEventProcessor
{
    private readonly Dictionary<IntegrationType, IPersonalNotificationDispatcher<AlertNotificationContext>> _personal =
        personalDispatchers.ToDictionary(d => d.Type);
    private readonly Dictionary<IntegrationType, IChannelNotificationDispatcher<AlertNotificationContext>> _channel =
        channelDispatchers.ToDictionary(d => d.Type);
    private readonly Dictionary<IntegrationType, ISystemEventDispatcher> _systemEvent =
        systemEventDispatchers.ToDictionary(d => d.Type);

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
        var matched = subscriptions
            .Where(s => s.Events().Contains(outboxRow.EventType) && alert.Severity >= s.MinSeverity)
            .ToList();

        logger.LogInformation("[notify] {EventType} (alert #{AlertId}, {Severity}) matched {Count} subscription(s).",
            outboxRow.EventType, alert.AlertId, alert.Severity, matched.Count);

        foreach (var sub in matched)
        {
            if (ct.IsCancellationRequested) break;
            await DeliverAsync(sub, outboxRow, alert, ct);
        }
    }

    private async Task DeliverAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, CancellationToken ct)
    {
        // Deterministic per (event × subscription) so a retried outbox row can't double-deliver.
        var idempotencyKey = $"{row.EventType}:{alert.AlertId}:{sub.Id}";
        if (await deliveryLogRepo.ExistsAsync(idempotencyKey, ct)) return;

        switch (sub.TargetKind)
        {
            case NotificationTargetKind.Personal:
                await DeliverPersonalAsync(sub, row, alert, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Channel:
                await DeliverChannelAsync(sub, row, alert, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Integration:
                await DeliverIntegrationAsync(sub, row, alert, idempotencyKey, ct);
                break;
        }
    }

    private async Task DeliverPersonalAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, string idempotencyKey, CancellationToken ct)
    {
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

                logger.LogInformation("[notify] Delivered Personal → {Descriptor} for {EventType} (sub \"{Name}\").",
                    descriptor, row.EventType, sub.Name);
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

    // Channel (mode 2): post to a team channel via the subscription's integration (e.g. Google Chat).
    private async Task DeliverChannelAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, string idempotencyKey, CancellationToken ct)
    {
        if (sub.Integration is null || !_channel.TryGetValue(sub.Integration.Type, out var dispatcher))
        {
            await RecordFailedAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? sub.TargetKind.ToString(),
                "No group dispatcher registered for this integration type.", ct);
            return;
        }

        var context = BuildContext(alert);
        var descriptor = sub.Integration.Name + (sub.Target is { Length: > 0 } t ? $" · {t}" : "");
        try
        {
            var sent = await dispatcher.SendAsync(sub.Integration, sub.Target, context, ct);
            await RecordAsync(sub, row, idempotencyKey, descriptor,
                sent ? DeliveryStatus.Delivered : DeliveryStatus.Failed,
                sent ? null : "Group dispatcher reported failure.", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Group delivery failed for subscription {Sub} via {Type}.", sub.Id, sub.Integration.Type);
            await RecordFailedAsync(sub, row, idempotencyKey, descriptor, ex.Message, ct);
        }
    }

    // Integration (mode 3): hand the event to an external platform (e.g. PagerDuty). The routing key is
    // resolved per-service from the RFC-0004 ServiceIntegrationMapping — each service pages its own
    // PagerDuty service. A trigger opens the event; a resolve closes it, keyed by the same dedup key.
    private async Task DeliverIntegrationAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, string idempotencyKey, CancellationToken ct)
    {
        if (sub.Integration is null || !_systemEvent.TryGetValue(sub.Integration.Type, out var dispatcher))
        {
            await RecordFailedAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? sub.TargetKind.ToString(),
                "No system-event dispatcher registered for this integration type.", ct);
            return;
        }

        // An orphan/external alert has no Piro service, so there is nothing to map to a routing key.
        if (alert.ServiceId is null)
        {
            await RecordSkippedAsync(sub, row, idempotencyKey, sub.Integration.Name,
                "Alert has no service; cannot resolve a per-service routing key.", ct);
            return;
        }

        var mapping = await mappingRepo.GetAsync(alert.ServiceId.Value, sub.Integration.Id, ct);
        var routingKey = ExtractRoutingKey(mapping?.MappingJson);
        if (routingKey is null)
        {
            await RecordSkippedAsync(sub, row, idempotencyKey, sub.Integration.Name,
                "No routing key mapped for this service and integration.", ct);
            return;
        }

        // Stable dedup key per logical alert so a later resolve references the same remote event.
        var dedupKey = $"piro-alert-{alert.AlertId}";
        var descriptor = $"{sub.Integration.Name} (service {alert.ServiceId})";
        try
        {
            var ok = alert.IsRecovery
                ? await dispatcher.ResolveAsync(routingKey, dedupKey, ct)
                : await dispatcher.TriggerAsync(routingKey, dedupKey, BuildContext(alert), ct);
            await RecordAsync(sub, row, idempotencyKey, descriptor,
                ok ? DeliveryStatus.Delivered : DeliveryStatus.Failed,
                ok ? null : "System-event dispatcher reported failure.", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration delivery failed for subscription {Sub} via {Type}.", sub.Id, sub.Integration.Type);
            await RecordFailedAsync(sub, row, idempotencyKey, descriptor, ex.Message, ct);
        }
    }

    // PagerDuty mapping stores { "pagerDutyServiceId": "...", "routingKey": "..." } (RFC 0004 §4.5).
    private static string? ExtractRoutingKey(string? mappingJson)
    {
        if (string.IsNullOrWhiteSpace(mappingJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(mappingJson);
            return doc.RootElement.TryGetProperty("routingKey", out var k) ? k.GetString() : null;
        }
        catch { return null; }
    }

    private Task RecordAsync(NotificationSubscription sub, NotificationEventOutbox row, string key, string descriptor, DeliveryStatus status, string? error, CancellationToken ct)
    {
        logger.LogInformation("[notify] {Status} {TargetKind} → {Descriptor} for {EventType} (sub \"{Name}\").{Error}",
            status, sub.TargetKind, descriptor, row.EventType, sub.Name, error is null ? "" : $" {error}");

        return deliveryLogRepo.RecordAsync(new NotificationDeliveryLog
        {
            IdempotencyKey = key,
            EventType = row.EventType,
            SubscriptionId = sub.Id,
            TargetKind = sub.TargetKind.ToString(),
            TargetDescriptor = descriptor,
            Status = status,
            Error = error,
            AttemptedAt = DateTime.UtcNow,
        }, ct);
    }

    private Task RecordFailedAsync(NotificationSubscription sub, NotificationEventOutbox row, string key, string descriptor, string error, CancellationToken ct) =>
        RecordAsync(sub, row, key, descriptor, DeliveryStatus.Failed, error, ct);

    private Task RecordSkippedAsync(NotificationSubscription sub, NotificationEventOutbox row, string key, string descriptor, string reason, CancellationToken ct) =>
        RecordAsync(sub, row, key, descriptor, DeliveryStatus.Skipped, reason, ct);

    // Minimal snapshot the matcher needs, unified across the three alert payload shapes.
    private sealed record AlertSnapshot(int AlertId, int? ServiceId, string ServiceName, string CheckName, AlertSeverity Severity, bool IsRecovery);

    private static AlertSnapshot? TryDeserializeAlert(NotificationEventOutbox row)
    {
        switch (row.EventType)
        {
            case "alert:created":
            {
                var p = JsonSerializer.Deserialize<AlertCreatedPayload>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case "alert:acknowledged":
            {
                var p = JsonSerializer.Deserialize<AlertAcknowledgedPayload>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case "alert:resolved":
            {
                var p = JsonSerializer.Deserialize<AlertResolvedPayload>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: true);
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
