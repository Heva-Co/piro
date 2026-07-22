using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Generic outbound webhook dispatcher (RFC 0015). POSTs (or PUTs) a fixed, versioned JSON payload to
/// the configured URL when a subscribed alert or incident event fires. A channel dispatcher: the
/// destination is the config URL, so <c>target</c> is unused. The body is not user-editable — Piro owns
/// the schema, so Zapier/Make map against a stable shape.
/// </summary>
public class WebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger, ISecretProtector secretProtector)
    : IChannelNotificationDispatcher<AlertNotificationContext>,
      IChannelNotificationDispatcher<IncidentNotificationContext>
{
    /// <summary>The public payload contract version. Bump only on a breaking change; evolve additively otherwise.</summary>
    private const int SchemaVersion = 1;

    /// <summary>Headers Piro manages itself — a user-supplied custom header can never override these.</summary>
    private static readonly HashSet<string> ReservedHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Authorization", "Content-Type" };

    // camelCase property names + string enums, so the payload is a stable, human-mappable shape for
    // Zapier/Make. Note ServiceStatus members are already upper-snake (UP/DOWN/…), so they serialize
    // verbatim regardless of naming policy.
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public IntegrationType Type => IntegrationType.Webhook;

    public Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default) =>
        PostAsync(integration, BuildAlertEnvelope(content), ct);

    public Task<bool> SendAsync(Integration integration, string? target, IncidentNotificationContext content, CancellationToken ct = default) =>
        PostAsync(integration, BuildIncidentEnvelope(content), ct);

    private async Task<bool> PostAsync(Integration integration, object envelope, CancellationToken ct)
    {
        WebhookIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<WebhookIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector)); }
        catch { return false; }

        var payload = JsonSerializer.Serialize(envelope, PayloadOptions);
        var method = string.Equals(config.Method, "PUT", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Put : HttpMethod.Post;

        var client = httpClientFactory.CreateClient("piro-webhook");
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
            logger.LogWarning(ex, "Webhook request failed for integration {IntegrationId}.", integration.Id);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Webhook returned {Status}: {Body}", (int)response.StatusCode, body);
            return false;
        }

        logger.LogInformation("Webhook notification delivered for integration {IntegrationId}.", integration.Id);
        return true;
    }

    private static object BuildIncidentEnvelope(IncidentNotificationContext c) => new
    {
        schemaVersion = SchemaVersion,
        @event = c.IsResolved ? "incident.resolved" : "incident.opened",
        sentAt = DateTimeOffset.UtcNow,
        incident = new
        {
            id = c.IncidentId,
            title = c.Title,
            status = c.Status,
            isResolved = c.IsResolved,
            visibility = c.Visibility,
            affectedServices = c.AffectedServices,
            occurredAt = c.OccurredAt,
        },
    };

    private static object BuildAlertEnvelope(AlertNotificationContext c) => new
    {
        schemaVersion = SchemaVersion,
        @event = c.IsRecovery ? "alert.resolved" : "alert.created",
        sentAt = DateTimeOffset.UtcNow,
        alert = new
        {
            serviceName = c.ServiceName,
            checkName = c.CheckName,
            currentStatus = c.CurrentStatus,
            severity = c.Severity,
            isRecovery = c.IsRecovery,
            description = c.AlertDescription,
            firedAt = c.FiredAt,
            incidentUrl = c.IncidentUrl,
            serviceUrl = c.ServiceUrl,
            checkUrl = c.CheckUrl,
            alertUrl = c.AlertUrl,
        },
    };

    private record WebhookIntegrationConfig(
        [property: Required] string Url,
        string? Method,
        string? AuthorizationHeader,
        Dictionary<string, string>? CustomHeaders);
}
