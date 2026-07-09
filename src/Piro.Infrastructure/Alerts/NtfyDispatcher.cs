using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via ntfy.sh or a self-hosted ntfy instance.</summary>
public class NtfyDispatcher(IHttpClientFactory httpClientFactory, ILogger<NtfyDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Ntfy;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<NtfyChannelMeta>(channel.MetaJson);
        var (serverUrl, accessToken) = ResolveServerConfig(channel, meta);

        await SendAsync(serverUrl, meta.Topic, accessToken, meta.Priority, context, ct);
        logger.LogInformation("ntfy notification sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    public async Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        var config = JsonUtils.Deserialize<NtfyIntegrationConfig>(integration.ConfigJson);

        await SendAsync(config?.ServerUrl, handle, config?.Token, null, context, ct);
        logger.LogInformation("ntfy personal alert sent to topic {Topic}.", handle);
        return true;
    }

    private static (string serverUrl, string? accessToken) ResolveServerConfig(NotificationChannel channel, NtfyChannelMeta meta)
    {
        if (channel.Integration is not null)
        {
            var cfg = JsonUtils.Deserialize<NtfyIntegrationConfig>(channel.Integration.ConfigJson);
            if (cfg is not null)
                return (cfg.ServerUrl?.TrimEnd('/') ?? "https://ntfy.sh", cfg.Token);
        }
        // Legacy: server config in MetaJson
        return (meta.ServerUrl?.TrimEnd('/') ?? "https://ntfy.sh", meta.AccessToken);
    }

    private async Task SendAsync(string? serverUrl, string topic, string? accessToken, int? priorityOverride, AlertNotificationContext context, CancellationToken ct)
    {
        var url = $"{serverUrl?.TrimEnd('/') ?? "https://ntfy.sh"}/{topic}";

        var emoji = context.IsRecovery ? "✅" : context.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };
        var title = context.IsRecovery
            ? $"RECOVERED — {context.ServiceName} / {context.CheckName}"
            : $"{context.Severity.ToString().ToUpperInvariant()} — {context.ServiceName} / {context.CheckName}";
        var message = context.IsRecovery
            ? $"{context.CheckName} has recovered. Status: {context.CurrentStatus}."
            : $"{context.CheckName} is {context.CurrentStatus}.{(context.AlertDescription is not null ? $" {context.AlertDescription}" : "")}";
        var priority = priorityOverride ?? context.Severity switch
        {
            AlertSeverity.Critical => 5,
            AlertSeverity.Warning  => 4,
            _                      => 3
        };
        if (context.IsRecovery) priority = 3;

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(message, Encoding.UTF8, "text/plain");
        request.Headers.Add("Title", EncodeRfc2047(title));
        request.Headers.Add("Priority", priority.ToString());
        request.Headers.Add("Tags", context.IsRecovery ? "white_check_mark" : "rotating_light");
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("ntfy request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("ntfy returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"ntfy error {(int)response.StatusCode}: {body}");
        }
    }

    // RFC 2047 encoding allows non-ASCII characters in HTTP headers
    private static string EncodeRfc2047(string value) =>
        value.Any(c => c > 127)
            ? $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?="
            : value;

    private record NtfyChannelMeta(
        [property: Required] string Topic,
        string? ServerUrl = null,
        string? AccessToken = null,
        int? Priority = null);

    private record NtfyIntegrationConfig(string? ServerUrl = null, string? Token = null);
}
