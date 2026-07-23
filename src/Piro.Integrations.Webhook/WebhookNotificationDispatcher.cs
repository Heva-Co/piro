using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Webhook;

/// <summary>
/// Generic outbound webhook dispatcher (RFC 0015, migrated to its own assembly per RFC 0016). POSTs
/// (or PUTs) a fixed, versioned JSON payload to the configured URL when a subscribed alert or incident
/// event fires. A channel dispatcher: the destination is the config URL, so <c>delivery.Target</c> is
/// unused. The body is not user-editable — Piro owns the schema, so Zapier/Make map against a stable
/// shape. It reaches Piro only through <see cref="IIntegrationHost"/>: it asks the host for an
/// HttpClient and for its own decrypted <see cref="WebhookConfig"/>, and renders the neutral
/// <see cref="Event"/> itself — no Piro.Domain type, no repository, no secret store.
/// </summary>
public sealed class WebhookNotificationDispatcher : INotificationDispatcher
{
    /// <summary>The public payload contract version. Bump only on a breaking change; evolve additively otherwise.</summary>
    private const int SchemaVersion = 1;

    /// <summary>Headers Piro manages itself — a user-supplied custom header can never override these.</summary>
    private static readonly HashSet<string> ReservedHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Authorization", "Content-Type" };

    // camelCase property names + string enums, so the payload is a stable, human-mappable shape for
    // Zapier/Make.
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string IntegrationId => "Webhook";

    public async Task<bool> SendAsync(Event evt, NotificationDelivery delivery, IIntegrationHost host, CancellationToken ct = default)
    {
        if (delivery.IntegrationId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<WebhookConfig>(integrationId, ct);
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
            return false;

        var envelope = BuildEnvelope(evt);
        if (envelope is null)
            return false;

        return await PostAsync(host, config, envelope, ct);
    }

    private static async Task<bool> PostAsync(IIntegrationHost host, WebhookConfig config, object envelope, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(envelope, PayloadOptions);
        var method = string.Equals(config.Method, "PUT", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Put : HttpMethod.Post;

        var client = host.GetRequiredService<HttpClient>();
        using var request = new HttpRequestMessage(method, config.Url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (config.CustomHeaders is { Count: > 0 })
        {
            foreach (var (name, value) in config.CustomHeaders)
            {
                // Content-Type is set by the StringContent above; Authorization is managed by the
                // dedicated field below. User headers never override either.
                if (string.IsNullOrWhiteSpace(name) || ReservedHeaders.Contains(name))
                    continue;
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.AuthorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", config.AuthorizationHeader);

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        return response.IsSuccessStatusCode;
    }

    /// <summary>Selects and builds the versioned envelope for the neutral event, or null for an event this dispatcher does not render.</summary>
    private static object? BuildEnvelope(Event evt) => evt switch
    {
        AlertEvent alert => BuildAlertEnvelope(alert),
        IncidentEvent incident => BuildIncidentEnvelope(incident),
        _ => null,
    };

    private static object BuildIncidentEnvelope(IncidentEvent c) => new
    {
        schemaVersion = SchemaVersion,
        @event = c is IncidentResolvedEvent ? "incident.resolved" : "incident.opened",
        sentAt = DateTimeOffset.UtcNow,
        incident = new
        {
            id = c.IncidentId,
            title = c.Title,
            status = c.Status,
            isResolved = c is IncidentResolvedEvent,
            visibility = c.Visibility,
            affectedServices = c.AffectedServices,
            occurredAt = c.FiredAt,
        },
    };

    private static object BuildAlertEnvelope(AlertEvent c) => new
    {
        schemaVersion = SchemaVersion,
        @event = AlertEventName(c),
        sentAt = DateTimeOffset.UtcNow,
        alert = new
        {
            serviceName = c.ServiceName,
            checkName = c.CheckName,
            currentStatus = c.CurrentStatus,
            severity = c.Severity,
            isRecovery = c is AlertResolvedEvent,
            description = c.Description,
            firedAt = c.FiredAt,
            incidentUrl = (string?)null,
            serviceUrl = c.ServiceUrl,
            checkUrl = c.CheckUrl,
            alertUrl = c.Url,
        },
    };

    private static string AlertEventName(AlertEvent c) => c switch
    {
        AlertResolvedEvent => "alert.resolved",
        AlertAcknowledgedEvent => "alert.acknowledged",
        _ => "alert.created",
    };
}
