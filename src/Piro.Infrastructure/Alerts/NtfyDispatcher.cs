using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via ntfy.sh or a self-hosted ntfy instance.</summary>
public class NtfyDispatcher(IHttpClientFactory httpClientFactory, ILogger<NtfyDispatcher> logger, ISecretProtector secretProtector)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Ntfy;

    public async Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        var config = JsonUtils.Deserialize<NtfyIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector));

        await SendAsync(config?.ServerUrl, handle, config?.Token, null, context, ct);
        logger.LogInformation("ntfy personal alert sent to topic {Topic}.", handle);
        return true;
    }

    public async Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        var config = JsonUtils.Deserialize<NtfyIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector));

        var url = $"{config?.ServerUrl?.TrimEnd('/') ?? "https://ntfy.sh"}/{handle}";
        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(message, Encoding.UTF8, "text/plain");
        request.Headers.Add("Title", "Piro verification code");
        if (!string.IsNullOrWhiteSpace(config?.Token))
            request.Headers.Add("Authorization", $"Bearer {config.Token}");

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

        logger.LogInformation("ntfy verification message sent to topic {Topic}.", handle);
        return true;
    }

    private async Task SendAsync(string? serverUrl, string topic, string? accessToken, int? priorityOverride, AlertNotificationContext context, CancellationToken ct)
    {
        var url = $"{serverUrl?.TrimEnd('/') ?? "https://ntfy.sh"}/{topic}";

        var title = AlertMessageTemplates.NtfyTitle(context);
        var message = AlertMessageTemplates.NtfyBody(context);
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

    private record NtfyIntegrationConfig(string? ServerUrl = null, string? Token = null);
}
