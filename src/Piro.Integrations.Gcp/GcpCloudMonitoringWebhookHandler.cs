using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Gcp;

/// <summary>
/// Handles inbound GCP Cloud Monitoring webhook notifications (RFC 0001 §4.8, isolated per RFC 0016).
/// Everything GCP-specific lives here: the query-string token check, parsing the notification payload,
/// and mapping severity. It writes into Piro only through <see cref="IAlertService"/> resolved from the
/// host — it never touches a repository or Piro's alert service directly. A closed incident resolves the
/// matching alert; an open one records an occurrence.
/// </summary>
public sealed class GcpCloudMonitoringWebhookHandler : IInboundWebhookHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string IntegrationId => "GcpCloudMonitoringWebhook";

    // GCP's webhook URL carries no extra path parameters — the instance id in the URL is enough.
    public string WebhookPathTemplate => "";

    public async Task<WebhookOutcome> HandleAsync(InboundWebhookContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        // GCP can't send custom headers, so its token rides the query string (?auth_token=...).
        var authToken = ctx.Query.GetValueOrDefault("auth_token");
        var config = await host.GetConfigAsync<GcpCloudMonitoringWebhookConfig>(ctx.IntegrationId, ct);
        if (!IsAuthorized(config?.AuthToken, authToken))
            return WebhookOutcome.AuthFailed;

        GcpCloudMonitoringWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GcpCloudMonitoringWebhookPayload>(ctx.RawPayload, JsonOptions);
        }
        catch (JsonException)
        {
            return WebhookOutcome.ParseError;
        }

        var incident = payload?.Incident;
        if (incident is null || string.IsNullOrWhiteSpace(incident.IncidentId) || string.IsNullOrWhiteSpace(incident.State))
            return WebhookOutcome.ParseError;

        var alerts = host.GetRequiredService<IAlertService>();

        if (incident.State == GcpIncidentState.Closed)
        {
            await alerts.ResolveOccurrenceAsync(ctx.IntegrationId, incident.IncidentId, ct);
            return WebhookOutcome.Accepted;
        }

        await alerts.RecordOccurrenceAsync(
            ctx.IntegrationId,
            incident.IncidentId,
            MapSeverity(incident.Severity),
            incident.Summary,
            incident.Url,
            ct);

        return WebhookOutcome.Accepted;
    }

    /// <summary>Constant-time comparison of the configured token against the one on the request.</summary>
    private static bool IsAuthorized(string? expected, string? actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
            return false;
        var e = Encoding.UTF8.GetBytes(expected);
        var a = Encoding.UTF8.GetBytes(actual);
        return e.Length == a.Length && CryptographicOperations.FixedTimeEquals(e, a);
    }

    /// <summary>critical → Down, everything else → Degraded (RFC 0001 §4.8).</summary>
    private static AlertImpact MapSeverity(string? severity) =>
        severity?.ToLowerInvariant() == "critical" ? AlertImpact.Down : AlertImpact.Degraded;
}
