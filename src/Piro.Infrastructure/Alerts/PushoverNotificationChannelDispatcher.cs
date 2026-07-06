using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via the Pushover API.</summary>
public class PushoverNotificationChannelDispatcher(IHttpClientFactory httpClientFactory, ILogger<PushoverNotificationChannelDispatcher> logger)
    : INotificationChannelDispatcher
{
    private const string PushoverApiUrl = "https://api.pushover.net/1/messages.json";

    public IntegrationType Type => IntegrationType.Pushover;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<PushoverTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Pushover trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.AppToken) || string.IsNullOrWhiteSpace(meta.UserKey))
        {
            logger.LogWarning("Pushover channel {ChannelId} is missing app token or user key.", channel.Id);
            return;
        }

        var emoji = context.IsRecovery ? "✅" : context.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var title = context.IsRecovery
            ? $"{emoji} RECOVERED — {context.ServiceName}"
            : $"{emoji} {context.Severity.ToString().ToUpperInvariant()} — {context.ServiceName}";

        var message = context.IsRecovery
            ? $"{context.CheckName} has recovered. Status: {context.CurrentStatus}."
            : $"{context.CheckName} is {context.CurrentStatus}. Severity: {context.Severity}.{(context.AlertDescription is not null ? $" {context.AlertDescription}" : "")}";

        // Map priority: default 0 (Normal), Critical → 1 (High), or use configured value
        var priority = meta.Priority ?? (context.Severity == AlertSeverity.Critical && !context.IsRecovery ? 1 : 0);

        var payload = new
        {
            token = meta.AppToken,
            user = meta.UserKey,
            title,
            message,
            priority,
            timestamp = new DateTimeOffset(context.FiredAt).ToUnixTimeSeconds()
        };

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, PushoverApiUrl);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Pushover request timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Pushover request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Pushover returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Pushover error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Pushover notification sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private record PushoverTriggerMeta(string AppToken, string UserKey, int? Priority = null);
}
