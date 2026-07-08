using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via the Pushover API.</summary>
public class PushoverDispatcher(IHttpClientFactory httpClientFactory, ILogger<PushoverDispatcher> logger)
    : INotificationDispatcher
{
    private const string ApiUrl = "https://api.pushover.net/1/messages.json";

    public IntegrationType Type => IntegrationType.Pushover;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<PushoverChannelMeta>(channel.MetaJson);
        var appToken = ResolveAppToken(channel);

        await SendAsync(appToken, meta.UserKey, meta.Priority, context, ct);
        logger.LogInformation("Pushover notification sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    public async Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        PushoverIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<PushoverIntegrationConfig>(integration.ConfigJson); }
        catch { return false; }

        await SendAsync(config.AppToken, handle, null, context, ct);
        logger.LogInformation("Pushover personal alert sent to {UserKey}.", handle);
        return true;
    }

    private static string ResolveAppToken(NotificationChannel channel)
    {
        if (channel.Integration is not null)
        {
            var cfg = JsonUtils.DeserializeAndValidate<PushoverIntegrationConfig>(channel.Integration.ConfigJson);
            return cfg.AppToken;
        }
        throw new InvalidOperationException();
    }

    private async Task SendAsync(string appToken, string userKey, int? priorityOverride, AlertNotificationContext context, CancellationToken ct)
    {
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
        var priority = priorityOverride ?? (context.Severity == AlertSeverity.Critical && !context.IsRecovery ? 1 : 0);

        var payload = new { token = appToken, user = userKey, title, message, priority, timestamp = new DateTimeOffset(context.FiredAt).ToUnixTimeSeconds() };

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Pushover request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Pushover returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Pushover error {(int)response.StatusCode}: {body}");
        }
    }

    private record PushoverChannelMeta([property: Required] string UserKey, int? Priority = null);

    private record PushoverIntegrationConfig([property: Required] string AppToken);
}
