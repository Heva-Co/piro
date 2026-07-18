using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Integrations.Config;

namespace Piro.Application.Services;

/// <summary>
/// Ingests inbound GCP Cloud Monitoring webhook notifications into <see cref="Alert"/> rows —
/// RFC 0001 §4.6/§4.8. Every request is logged via <see cref="WebhookRequestLog"/> before auth or
/// parsing, so rejected/malformed requests are observable too, not just successful ones.
/// Correlation (anchoring to a Check/Service via policy_name) is deferred — every Alert produced
/// here is currently orphan.
/// </summary>
public class GcpWebhookIngestionService(
    IIntegrationRepository integrationRepository,
    IWebhookRequestLogRepository webhookLogRepository,
    AlertLifecycleService alertLifecycleService,
    ISecretProtector secretProtector)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Processes one inbound POST. Returns the outcome for the controller to map to an HTTP status —
    /// never throws for a malformed/unauthenticated request, since GCP retries on non-2xx and we
    /// don't want a retry storm for a payload Piro will never parse successfully.
    /// </summary>
    public async Task<WebhookRequestOutcome> IngestAsync(
        Guid integrationId,
        string? authToken,
        string rawPayload,
        CancellationToken ct = default)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId, ct);

        var log = new WebhookRequestLog
        {
            IntegrationId = integrationId,
            ReceivedAt = DateTimeOffset.UtcNow,
            RawPayload = rawPayload,
        };

        if (integration is null || integration.Type != IntegrationType.GcpCloudMonitoringWebhook)
        {
            // Nothing to attribute this request to — still not written, there's no valid IntegrationId
            // to log against. GCP gets a generic 404 via the controller.
            return WebhookRequestOutcome.AuthFailed;
        }

        if (!TryValidateToken(integration.ReadDecryptedConfigJson(secretProtector), authToken))
        {
            log.Outcome = WebhookRequestOutcome.AuthFailed;
            await webhookLogRepository.CreateAsync(log, ct);
            return WebhookRequestOutcome.AuthFailed;
        }

        GcpCloudMonitoringWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GcpCloudMonitoringWebhookPayload>(rawPayload, JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }

        var incident = payload?.Incident;
        if (incident is null || string.IsNullOrWhiteSpace(incident.IncidentId) || string.IsNullOrWhiteSpace(incident.State))
        {
            log.Outcome = WebhookRequestOutcome.ParseError;
            await webhookLogRepository.CreateAsync(log, ct);
            return WebhookRequestOutcome.ParseError;
        }

        log.Outcome = WebhookRequestOutcome.AcceptedOrphan;
        var created = await webhookLogRepository.CreateAsync(log, ct);

        if (incident.State == GcpIncidentState.Closed)
        {
            var resolved = await alertLifecycleService.ResolveExternalOccurrenceAsync(
                AlertSource.GcpCloudMonitoring, incident.IncidentId, ct);

            if (resolved is not null)
            {
                created.AlertId = resolved.Id;
                await webhookLogRepository.UpdateAsync(created, ct);
            }

            return WebhookRequestOutcome.AcceptedOrphan;
        }

        var alert = await alertLifecycleService.RecordExternalOccurrenceAsync(
            source: AlertSource.GcpCloudMonitoring,
            externalId: incident.IncidentId,
            check: null,
            service: null,
            message: incident.Summary,
            impact: MapSeverity(incident.Severity),
            escalationPolicyId: integration.EscalationPolicyId,
            sourceRequestLogId: created.Id,
            sourceUrl: incident.Url,
            ct: ct);

        created.AlertId = alert.Id;
        await webhookLogRepository.UpdateAsync(created, ct);

        return WebhookRequestOutcome.AcceptedOrphan;
    }

    /// <summary>Constant-time comparison against the Integration's stored auth token — see <see cref="GcpCloudMonitoringWebhookConfig"/>.</summary>
    private static bool TryValidateToken(string configJson, string? authToken)
    {
        if (string.IsNullOrEmpty(authToken))
            return false;

        GcpCloudMonitoringWebhookConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<GcpCloudMonitoringWebhookConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (config is null || string.IsNullOrEmpty(config.AuthToken))
            return false;

        var expected = Encoding.UTF8.GetBytes(config.AuthToken);
        var actual = Encoding.UTF8.GetBytes(authToken);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>critical→DOWN, warning→DEGRADED, unrecognized→DEGRADED — RFC 0001 §4.8.</summary>
    private static ServiceStatus MapSeverity(string? severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => ServiceStatus.DOWN,
        _ => ServiceStatus.DEGRADED,
    };
}
