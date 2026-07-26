using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models.NotificationEvents;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>
/// Maps an alert lifecycle event to its contracted payload (RFC 0009 §4.2/§4.3) and publishes it. The
/// payload contract lives entirely here — call sites pass only the alert and the event. Each field is
/// read from the alert's loaded navigations (via <see cref="AlertExtensions"/>) and copied into the
/// immutable payload record synchronously, so the snapshot reflects the moment of the call and no
/// later mutation or reload can change it.
/// </summary>
public class AlertNotificationPublisher(INotificationEventPublisher publisher, TagAppService tagApp) : IAlertNotificationPublisher
{
    public async Task PublishAsync(Alert alert, NotificationEventType evt, CancellationToken ct = default)
    {
        // The service's effective tags at emit time — the tag axis a subscription filter matches on
        // (RFC 0008, #203). Read here rather than from the alert's navigations because tags live on the
        // Service, not the Alert; an orphan/external alert (no ServiceId) carries no tags.
        var tags = await ResolveServiceTagsAsync(alert.ServiceId, ct);

        INotificationEvent payload = evt switch
        {
            NotificationEventType.AlertCreated => new AlertCreatedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                tags,
                alert.IsExternal(),
                alert.ExternalSourceLabel(),
                alert.FiredAt,
                ServiceId: alert.ServiceId
            ),

            NotificationEventType.AlertAcknowledged => new AlertAcknowledgedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                tags,
                alert.AcknowledgedBy,
                DateTimeOffset.UtcNow,
                ServiceId: alert.ServiceId
            ),

            NotificationEventType.AlertResolved => new AlertResolvedPayload(
                alert.Id,
                alert.ServiceLabel(),
                alert.CheckLabel(),
                alert.SeverityOrDefault(),
                tags,
                alert.ResolvedAt ?? DateTimeOffset.UtcNow,
                ServiceId: alert.ServiceId
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Not an alert lifecycle event."),
        };

        // All events of one alert share an ordering key so they reach each destination in emit order.
        await publisher.PublishAsync(payload, $"alert:{alert.Id}", ct);
    }

    /// <summary>The service's effective tags as a key → value map (value null for a key-only tag).
    /// Empty for an orphan/external alert or a service that vanished between emit and read.</summary>
    private async Task<IReadOnlyDictionary<string, string?>> ResolveServiceTagsAsync(int? serviceId, CancellationToken ct)
    {
        if (serviceId is null) return EmptyTags;
        try
        {
            var result = await tagApp.GetServiceTagsAsync(serviceId.Value, ct);
            var map = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var t in result.Tags) map[t.Key] = t.Value; // last write wins on a duplicate key
            return map;
        }
        catch (NotFoundException)
        {
            // Service deleted between the alert firing and this read — treat as no tags rather than
            // failing the whole notification emit.
            return EmptyTags;
        }
    }

    private static readonly IReadOnlyDictionary<string, string?> EmptyTags =
        new Dictionary<string, string?>(0);
}
