using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Sends alert lifecycle events to PagerDuty via the Events API v2 (RFC 0004). A failing check opens
/// an event (<c>trigger</c>); its recovery closes it (<c>resolve</c>). PagerDuty groups events into
/// incidents and runs its own escalation/paging — Piro only opens and closes.
/// </summary>
public class PagerDutyDispatcher(IHttpClientFactory httpClientFactory, ILogger<PagerDutyDispatcher> logger)
    : ISystemEventDispatcher
{
    private const string EnqueueUrl = "https://events.pagerduty.com/v2/enqueue";
    private const int MaxSummaryLength = 1024;

    public string IntegrationId => "PagerDuty";

    public Task<bool> TriggerAsync(string routingKey, string dedupKey, AlertNotificationContext context, CancellationToken ct = default)
    {
        var payload = new
        {
            routing_key = routingKey,
            event_action = "trigger",
            dedup_key = dedupKey,
            client = "Piro",
            client_url = context.AlertUrl,
            payload = new
            {
                summary = Truncate(context.Title(), MaxSummaryLength),
                source = context.ServiceName,
                severity = MapSeverity(context.Severity),
                timestamp = context.FiredAt.ToUniversalTime().ToString("o"),
                component = context.CheckName,
                custom_details = new
                {
                    service = context.ServiceName,
                    check = context.CheckName,
                    status = context.CurrentStatus.ToString(),
                    value = context.AlertValue,
                },
            },
        };

        return SendAsync(payload, dedupKey, "trigger", ct);
    }

    public Task<bool> ResolveAsync(string routingKey, string dedupKey, CancellationToken ct = default)
    {
        // Resolve references the existing alert by dedup_key only — no payload.
        var payload = new
        {
            routing_key = routingKey,
            event_action = "resolve",
            dedup_key = dedupKey,
        };

        return SendAsync(payload, dedupKey, "resolve", ct);
    }

    /// <summary>Maps Piro's two-level severity to PagerDuty's enum. Recovery isn't sent as an event severity (resolve carries none).</summary>
    private static string MapSeverity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "critical",
        AlertSeverity.Warning => "warning",
        _ => "warning",
    };

    /// <summary>
    /// POSTs to the Events API. Returns true on a 202; false (logged) on a clean failure — never
    /// throws, so one bad routing key can't break alert processing (RFC 0004 §4.7). A 429/5xx is
    /// retried once with backoff before giving up.
    /// </summary>
    private async Task<bool> SendAsync(object payload, string dedupKey, string action, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("piro-webhook");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, EnqueueUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "PagerDuty {Action} request failed for dedup_key {DedupKey}.", action, dedupKey);
                return false;
            }

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("PagerDuty {Action} accepted for dedup_key {DedupKey}.", action, dedupKey);
                return true;
            }

            // Transient — retry once with backoff (honoring Retry-After when present).
            if (attempt == 0 && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500))
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                logger.LogWarning("PagerDuty {Action} got {Status}; retrying after {Delay}s.", action, (int)response.StatusCode, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("PagerDuty {Action} failed with {Status} for dedup_key {DedupKey}: {Body}",
                action, (int)response.StatusCode, dedupKey, body);
            return false;
        }

        return false;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
