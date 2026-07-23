using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Attributes;
using Piro.Contracts;
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
    IEnumerable<IPersonalNotificationDispatcher<IncidentNotificationContext>> incidentPersonalDispatchers,
    IEnumerable<IChannelNotificationDispatcher<IncidentNotificationContext>> incidentChannelDispatchers,
    IEnumerable<ISystemEventDispatcher> systemEventDispatchers,
    ILogger<SubscriptionMatchingProcessor> logger) : INotificationEventProcessor
{
    private readonly Dictionary<string, IPersonalNotificationDispatcher<AlertNotificationContext>> _personal =
        personalDispatchers.ToDictionary(d => d.IntegrationId);
    private readonly Dictionary<string, IChannelNotificationDispatcher<AlertNotificationContext>> _channel =
        channelDispatchers.ToDictionary(d => d.IntegrationId);
    private readonly Dictionary<string, IPersonalNotificationDispatcher<IncidentNotificationContext>> _incidentPersonal =
        incidentPersonalDispatchers.ToDictionary(d => d.IntegrationId);
    private readonly Dictionary<string, IChannelNotificationDispatcher<IncidentNotificationContext>> _incidentChannel =
        incidentChannelDispatchers.ToDictionary(d => d.IntegrationId);
    private readonly Dictionary<string, ISystemEventDispatcher> _systemEvent =
        systemEventDispatchers.ToDictionary(d => d.IntegrationId);

    public async Task ProcessAsync(NotificationEventOutbox outboxRow, CancellationToken ct = default)
    {
        var eventType = NotificationEventTypeExtensions.FromWireName(outboxRow.EventType);
        if (eventType is null)
        {
            logger.LogWarning("Outbox #{Id} has unknown event type {EventType}; skipping.", outboxRow.Id, outboxRow.EventType);
            return;
        }

        // Route by event family. Alerts gate on severity; incidents don't (they have none). Other
        // events (system:*) drain to Done as no-ops until their handlers land.
        var alert = TryDeserializeAlert(outboxRow);
        if (alert is not null) { await ProcessAlertAsync(outboxRow, alert, ct); return; }

        var incident = TryDeserializeIncident(outboxRow);
        if (incident is not null) { await ProcessIncidentAsync(outboxRow, incident, ct); return; }
    }

    private async Task ProcessAlertAsync(NotificationEventOutbox outboxRow, AlertSnapshot alert, CancellationToken ct)
    {
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

    private async Task ProcessIncidentAsync(NotificationEventOutbox outboxRow, IncidentSnapshot incident, CancellationToken ct)
    {
        // No severity gate for incidents — match on event membership only.
        var subscriptions = await subscriptionRepo.GetEnabledAsync(ct);
        var matched = subscriptions.Where(s => s.Events().Contains(outboxRow.EventType)).ToList();

        logger.LogInformation("[notify] {EventType} (incident #{IncidentId}) matched {Count} subscription(s).",
            outboxRow.EventType, incident.IncidentId, matched.Count);

        var context = new IncidentNotificationContext(
            incident.IncidentId, incident.Title, incident.Status, incident.IsResolved,
            incident.Visibility, incident.AffectedServices, DateTimeOffset.UtcNow);

        foreach (var sub in matched)
        {
            if (ct.IsCancellationRequested) break;
            await DeliverIncidentAsync(sub, outboxRow, incident, context, ct);
        }
    }

    private async Task DeliverIncidentAsync(NotificationSubscription sub, NotificationEventOutbox row, IncidentSnapshot incident, IncidentNotificationContext context, CancellationToken ct)
    {
        var idempotencyKey = $"{row.EventType}:{incident.IncidentId}:{sub.Id}";
        if (await deliveryLogRepo.ExistsAsync(idempotencyKey, ct)) return;

        // Incidents deliver to Personal and Channel destinations; Integration (paging platforms) is not
        // wired for incidents in v1.
        switch (sub.TargetKind)
        {
            case NotificationTargetKind.Personal:
                await DeliverIncidentPersonalAsync(sub, row, context, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Channel:
                await DeliverIncidentChannelAsync(sub, row, context, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Integration:
                await RecordSkippedAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? "Integration",
                    "Incident delivery to integration platforms is not supported in v1.", ct);
                break;
        }
    }

    private async Task DeliverIncidentPersonalAsync(NotificationSubscription sub, NotificationEventOutbox row, IncidentNotificationContext context, string idempotencyKey, CancellationToken ct)
    {
        if (sub.UserId is null) return;
        var prefs = (await prefRepo.GetByUserIdsAsync([sub.UserId.Value], ct)).GetValueOrDefault(sub.UserId.Value, []);

        foreach (var pref in prefs.OrderBy(p => p.Priority))
        {
            if (!pref.VerifiedAt.HasValue) continue;
            if (pref.Channel.RequiresIntegration() && pref.Integration is null) continue;
            var channelType = pref.Channel.ToIntegrationType();
            if (!_incidentPersonal.TryGetValue(channelType.ToString(), out var dispatcher)) continue;

            try
            {
                if (await dispatcher.SendAsync(pref.Integration, pref.Handle, context, ct))
                {
                    await RecordAsync(sub, row, idempotencyKey, $"{channelType}:{pref.Handle}", DeliveryStatus.Delivered, null, ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Incident personal delivery failed for sub {Sub} via {Channel}.", sub.Id, channelType);
            }
        }

        await RecordAsync(sub, row, idempotencyKey, $"user:{sub.UserId}", DeliveryStatus.Failed,
            "No verified personal preference could carry the incident.", ct);
    }

    private async Task DeliverIncidentChannelAsync(NotificationSubscription sub, NotificationEventOutbox row, IncidentNotificationContext context, string idempotencyKey, CancellationToken ct)
    {
        if (sub.Integration is null || !_incidentChannel.TryGetValue(sub.Integration.Type.ToString(), out var dispatcher))
        {
            await RecordFailedAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? "Channel",
                "No channel dispatcher registered for this integration type carries incidents.", ct);
            return;
        }

        var descriptor = sub.Integration.Name + (sub.Target is { Length: > 0 } t ? $" · {t}" : "");
        try
        {
            var sent = await dispatcher.SendAsync(sub.Integration, sub.Target, context, ct);
            await RecordAsync(sub, row, idempotencyKey, descriptor,
                sent ? DeliveryStatus.Delivered : DeliveryStatus.Failed,
                sent ? null : "Channel dispatcher reported failure.", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Incident channel delivery failed for sub {Sub} via {Type}.", sub.Id, sub.Integration.Type);
            await RecordFailedAsync(sub, row, idempotencyKey, descriptor, ex.Message, ct);
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
            if (!_personal.TryGetValue(channelType.ToString(), out var dispatcher)) continue;

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
                    IntegrationType = channelType,
                    // Personal channels that need an integration (Telegram/Twilio/Ntfy/Pushover) carry its
                    // instance id; self-sufficient ones (Email) leave it null.
                    IntegrationId = pref.Integration?.Id,
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
        if (sub.Integration is null || !_channel.TryGetValue(sub.Integration.Type.ToString(), out var dispatcher))
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
        if (sub.Integration is null || !_systemEvent.TryGetValue(sub.Integration.Type.ToString(), out var dispatcher))
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
            // Channel/Integration deliveries carry the integration's type (for its icon) and id (to
            // filter the feed by integration). Null for Personal — no integration instance.
            IntegrationType = sub.Integration?.Type,
            IntegrationId = sub.Integration?.Id,
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

    private sealed record IncidentSnapshot(int IncidentId, string Title, IncidentStatus Status, bool IsResolved,
        IncidentVisibility Visibility, IReadOnlyList<string> AffectedServices);

    private static IncidentSnapshot? TryDeserializeIncident(NotificationEventOutbox row)
    {
        switch (row.EventType)
        {
            case NotificationEventNames.IncidentCreated:
            {
                var p = JsonSerializer.Deserialize<IncidentCreatedPayload>(row.PayloadJson);
                return p is null ? null : new IncidentSnapshot(p.IncidentId, p.Title, p.Status, IsResolved: false, p.Visibility, p.AffectedServices);
            }
            case NotificationEventNames.IncidentResolved:
            {
                var p = JsonSerializer.Deserialize<IncidentResolvedPayload>(row.PayloadJson);
                return p is null ? null : new IncidentSnapshot(p.IncidentId, p.Title, p.Status, IsResolved: true, p.Visibility, p.AffectedServices);
            }
            default:
                return null;
        }
    }

    private static AlertSnapshot? TryDeserializeAlert(NotificationEventOutbox row)
    {
        switch (row.EventType)
        {
            case NotificationEventNames.AlertCreated:
            {
                var p = JsonSerializer.Deserialize<AlertCreatedPayload>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case NotificationEventNames.AlertAcknowledged:
            {
                var p = JsonSerializer.Deserialize<AlertAcknowledgedPayload>(row.PayloadJson);
                return p is null ? null : new AlertSnapshot(p.AlertId, p.ServiceId, p.ServiceName, p.CheckName, p.Severity, IsRecovery: false);
            }
            case NotificationEventNames.AlertResolved:
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
