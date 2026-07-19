using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Posts an alert notification to a Google Chat space via its incoming-webhook URL (RFC 0009 mode 2).
/// The whole space is the audience, so this is a group dispatcher — there is no per-person handle.
/// </summary>
public class GoogleChatDispatcher(IHttpClientFactory httpClientFactory, ILogger<GoogleChatDispatcher> logger, ISecretProtector secretProtector)
    : IChannelNotificationDispatcher<AlertNotificationContext>
{
    public IntegrationType Type => IntegrationType.GoogleChat;

    public async Task<bool> SendAsync(Integration integration, string? target, AlertNotificationContext content, CancellationToken ct = default)
    {
        GoogleChatIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<GoogleChatIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector)); }
        catch { return false; }

        // Google Chat's simplest supported payload: a plain-text message to the space the webhook targets.
        // `target` is unused — the incoming-webhook URL is already space-specific.
        var payload = JsonSerializer.Serialize(new { text = BuildMessage(content) });

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, config.WebhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Google Chat request failed for integration {IntegrationId}.", integration.Id);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Google Chat returned {Status}: {Body}", (int)response.StatusCode, body);
            return false;
        }

        logger.LogInformation("Google Chat notification posted for integration {IntegrationId}.", integration.Id);
        return true;
    }

    private static string BuildMessage(AlertNotificationContext c)
    {
        var icon = c.IsRecovery ? "✅" : c.Severity == AlertSeverity.Critical ? "🔴" : "⚠️";
        var verb = c.IsRecovery ? "recovered" : "fired";
        var line = $"{icon} *{c.Title()}* {verb}";
        return c.AlertValue is { Length: > 0 } value ? $"{line}\n{value}" : line;
    }

    private record GoogleChatIntegrationConfig([property: Required] string WebhookUrl);
}
