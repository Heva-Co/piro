using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.NotificationEvents;
using Piro.Application.Notifications;
using Piro.Application.Extensions;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;
using Piro.Domain.Attributes;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Notifications;

/// <summary>
/// The real event processor (RFC 0009 §4.4, RFC 0016). Matches a drained event against enabled
/// subscriptions (by event membership + minimum severity) and delivers each match to its destination.
/// Personal and Channel destinations go through the single <see cref="INotificationDispatcher"/> an
/// integration exposes (resolved by IntegrationId, fed the neutral <see cref="Event"/> mapped from the
/// snapshot); Integration destinations (paging platforms like PagerDuty) still use
/// <see cref="ISystemEventDispatcher"/> with a per-service routing key from the RFC-0004
/// <see cref="ServiceIntegrationMapping"/>. Every attempt is logged with a deterministic idempotency
/// key so a retry can't double-send.
/// </summary>
internal class SubscriptionMatchingProcessor(
    INotificationSubscriptionRepository subscriptionRepo,
    IUserNotificationPreferenceRepository prefRepo,
    INotificationDeliveryLogRepository deliveryLogRepo,
    IServiceIntegrationMappingRepository mappingRepo,
    IEnumerable<INotificationDispatcher> dispatchers,
    IEnumerable<ISystemEventDispatcher> systemEventDispatchers,
    IIntegrationHost host,
    ILogger<SubscriptionMatchingProcessor> logger) : INotificationEventProcessor
{
    private readonly Dictionary<string, INotificationDispatcher> _dispatchers =
        dispatchers.ToDictionary(d => d.IntegrationId, StringComparer.Ordinal);
    private readonly Dictionary<string, ISystemEventDispatcher> _systemEvent =
        systemEventDispatchers.ToDictionary(d => d.IntegrationId, StringComparer.Ordinal);

    public async Task ProcessAsync(NotificationEventOutbox outboxRow, CancellationToken ct = default)
    {
        var eventType = NotificationEventTypeExtensions.FromWireName(outboxRow.EventType);
        if (eventType is null)
        {
            logger.LogWarning("Outbox #{Id} has unknown event type {EventType}; skipping.", outboxRow.Id, outboxRow.EventType);
            return;
        }

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

        var evt = BuildAlertContext(alert).ToEvent(outboxRow.EventType);
        foreach (var sub in matched)
        {
            if (ct.IsCancellationRequested) break;
            await DeliverAsync(sub, outboxRow, alert, evt, ct);
        }
    }

    private async Task ProcessIncidentAsync(NotificationEventOutbox outboxRow, IncidentSnapshot incident, CancellationToken ct)
    {
        var subscriptions = await subscriptionRepo.GetEnabledAsync(ct);
        var matched = subscriptions.Where(s => s.Events().Contains(outboxRow.EventType)).ToList();

        logger.LogInformation("[notify] {EventType} (incident #{IncidentId}) matched {Count} subscription(s).",
            outboxRow.EventType, incident.IncidentId, matched.Count);

        var context = new IncidentNotificationContext(
            incident.IncidentId, incident.Title, incident.Status, incident.IsResolved,
            incident.Visibility, incident.AffectedServices, DateTimeOffset.UtcNow);
        var evt = context.ToEvent(outboxRow.EventType);

        foreach (var sub in matched)
        {
            if (ct.IsCancellationRequested) break;
            await DeliverIncidentAsync(sub, outboxRow, incident, evt, ct);
        }
    }

    // ── Alerts ────────────────────────────────────────────────────────────────

    private async Task DeliverAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, Event evt, CancellationToken ct)
    {
        var idempotencyKey = $"{row.EventType}:{alert.AlertId}:{sub.Id}";
        if (await deliveryLogRepo.ExistsAsync(idempotencyKey, ct)) return;

        switch (sub.TargetKind)
        {
            case NotificationTargetKind.Personal:
                await DeliverPersonalAsync(sub, row, evt, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Channel:
                await DeliverChannelAsync(sub, row, evt, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Integration:
                await DeliverIntegrationAsync(sub, row, alert, idempotencyKey, ct);
                break;
        }
    }

    private async Task DeliverPersonalAsync(NotificationSubscription sub, NotificationEventOutbox row, Event evt, string idempotencyKey, CancellationToken ct)
    {
        if (sub.UserId is null) return;
        var prefs = (await prefRepo.GetByUserIdsAsync([sub.UserId.Value], ct)).GetValueOrDefault(sub.UserId.Value, []);

        foreach (var pref in prefs.OrderBy(p => p.Priority))
        {
            if (!pref.VerifiedAt.HasValue) continue;
            if (pref.Channel.RequiresIntegration() && pref.Integration is null) continue;
            var integrationId = pref.Channel.ToIntegrationId();
            if (!_dispatchers.TryGetValue(integrationId, out var dispatcher)) continue;

            var delivery = new NotificationDelivery { Target = pref.Handle, Mode = NotificationMode.Personal, IntegrationId = pref.Integration?.Id };
            var descriptor = $"{integrationId}:{pref.Handle}";
            try
            {
                if (await dispatcher.SendAsync(evt, delivery, host, ct))
                {
                    await RecordAsync(sub, row, idempotencyKey, descriptor, DeliveryStatus.Delivered, null, integrationId, pref.Integration?.Id, ct);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Personal delivery failed for sub {Sub} via {Channel}; trying next preference.", sub.Id, integrationId);
            }
        }

        await RecordAsync(sub, row, idempotencyKey, $"user:{sub.UserId}", DeliveryStatus.Failed,
            "No verified, supported personal preference delivered.", null, null, ct);
    }

    private async Task DeliverChannelAsync(NotificationSubscription sub, NotificationEventOutbox row, Event evt, string idempotencyKey, CancellationToken ct)
    {
        if (sub.Integration is null || !_dispatchers.TryGetValue(sub.Integration.Type, out var dispatcher))
        {
            await RecordAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? sub.TargetKind.ToString(),
                DeliveryStatus.Failed, "No dispatcher registered for this integration.", sub.Integration?.Type, sub.Integration?.Id, ct);
            return;
        }

        var delivery = new NotificationDelivery { Target = sub.Target ?? "", Mode = NotificationMode.Channel, IntegrationId = sub.Integration.Id };
        var descriptor = sub.Integration.Name + (sub.Target is { Length: > 0 } t ? $" · {t}" : "");
        try
        {
            var sent = await dispatcher.SendAsync(evt, delivery, host, ct);
            await RecordAsync(sub, row, idempotencyKey, descriptor, sent ? DeliveryStatus.Delivered : DeliveryStatus.Failed,
                sent ? null : "Channel dispatcher reported failure.", sub.Integration.Type, sub.Integration.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Channel delivery failed for subscription {Sub} via {Type}.", sub.Id, sub.Integration.Type);
            await RecordAsync(sub, row, idempotencyKey, descriptor, DeliveryStatus.Failed, ex.Message, sub.Integration.Type, sub.Integration.Id, ct);
        }
    }

    private async Task DeliverIntegrationAsync(NotificationSubscription sub, NotificationEventOutbox row, AlertSnapshot alert, string idempotencyKey, CancellationToken ct)
    {
        if (sub.Integration is null || !_systemEvent.TryGetValue(sub.Integration.Type, out var dispatcher))
        {
            await RecordAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? sub.TargetKind.ToString(),
                DeliveryStatus.Failed, "No system-event dispatcher registered for this integration.", sub.Integration?.Type, sub.Integration?.Id, ct);
            return;
        }

        if (alert.ServiceId is null)
        {
            await RecordAsync(sub, row, idempotencyKey, sub.Integration.Name, DeliveryStatus.Skipped,
                "Alert has no service; cannot resolve a per-service routing key.", sub.Integration.Type, sub.Integration.Id, ct);
            return;
        }

        var mapping = await mappingRepo.GetAsync(alert.ServiceId.Value, sub.Integration.Id, ct);
        var routingKey = ExtractRoutingKey(mapping?.MappingJson);
        if (routingKey is null)
        {
            await RecordAsync(sub, row, idempotencyKey, sub.Integration.Name, DeliveryStatus.Skipped,
                "No routing key mapped for this service and integration.", sub.Integration.Type, sub.Integration.Id, ct);
            return;
        }

        var dedupKey = $"piro-alert-{alert.AlertId}";
        var descriptor = $"{sub.Integration.Name} (service {alert.ServiceId})";
        try
        {
            var ok = alert.IsRecovery
                ? await dispatcher.ResolveAsync(routingKey, dedupKey, ct)
                : await dispatcher.TriggerAsync(routingKey, dedupKey, BuildAlertContext(alert), ct);
            await RecordAsync(sub, row, idempotencyKey, descriptor, ok ? DeliveryStatus.Delivered : DeliveryStatus.Failed,
                ok ? null : "System-event dispatcher reported failure.", sub.Integration.Type, sub.Integration.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration delivery failed for subscription {Sub} via {Type}.", sub.Id, sub.Integration.Type);
            await RecordAsync(sub, row, idempotencyKey, descriptor, DeliveryStatus.Failed, ex.Message, sub.Integration.Type, sub.Integration.Id, ct);
        }
    }

    // ── Incidents ─────────────────────────────────────────────────────────────

    private async Task DeliverIncidentAsync(NotificationSubscription sub, NotificationEventOutbox row, IncidentSnapshot incident, Event evt, CancellationToken ct)
    {
        var idempotencyKey = $"{row.EventType}:{incident.IncidentId}:{sub.Id}";
        if (await deliveryLogRepo.ExistsAsync(idempotencyKey, ct)) return;

        switch (sub.TargetKind)
        {
            case NotificationTargetKind.Personal:
                await DeliverPersonalAsync(sub, row, evt, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Channel:
                await DeliverChannelAsync(sub, row, evt, idempotencyKey, ct);
                break;
            case NotificationTargetKind.Integration:
                await RecordAsync(sub, row, idempotencyKey, sub.Integration?.Name ?? "Integration", DeliveryStatus.Skipped,
                    "Incident delivery to integration platforms is not supported in v1.", sub.Integration?.Type, sub.Integration?.Id, ct);
                break;
        }
    }

    // ── Shared ────────────────────────────────────────────────────────────────

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

    private Task RecordAsync(NotificationSubscription sub, NotificationEventOutbox row, string key, string descriptor,
        DeliveryStatus status, string? error, string? integrationType, Guid? integrationId, CancellationToken ct)
    {
        logger.LogInformation("[notify] {Status} {TargetKind} → {Descriptor} for {EventType} (sub \"{Name}\").{Error}",
            status, sub.TargetKind, descriptor, row.EventType, sub.Name, error is null ? "" : $" {error}");

        return deliveryLogRepo.RecordAsync(new NotificationDeliveryLog
        {
            IdempotencyKey = key,
            EventType = row.EventType,
            SubscriptionId = sub.Id,
            TargetKind = sub.TargetKind.ToString(),
            IntegrationType = integrationType,
            IntegrationId = integrationId,
            TargetDescriptor = descriptor,
            Status = status,
            Error = error,
            AttemptedAt = DateTime.UtcNow,
        }, ct);
    }

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

    private static AlertNotificationContext BuildAlertContext(AlertSnapshot a) => new(
        ServiceName: a.ServiceName,
        CheckName: a.CheckName,
        CurrentStatus: a.IsRecovery ? ServiceStatus.UP : ServiceStatus.DOWN,
        AlertDescription: null,
        Severity: a.Severity,
        IsRecovery: a.IsRecovery,
        FiredAt: DateTimeOffset.UtcNow,
        AlertId: a.AlertId);
}
