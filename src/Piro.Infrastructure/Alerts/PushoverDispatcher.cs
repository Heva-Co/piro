using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Contracts;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via the Pushover API.</summary>
public class PushoverDispatcher(IHttpClientFactory httpClientFactory, ILogger<PushoverDispatcher> logger, ISecretProtector secretProtector)
    : IPersonalNotificationDispatcher<AlertNotificationContext>
{
    private const string ApiUrl = "https://api.pushover.net/1/messages.json";

    public IntegrationType Type => IntegrationType.Pushover;

    public async Task<bool> SendAsync(Integration? integration, string handle, AlertNotificationContext content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        PushoverIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<PushoverIntegrationConfig>(integration.ReadDecryptedConfigJson(secretProtector)); }
        catch { return false; }

        await SendAsync(config.AppToken, handle, null, content, ct);
        logger.LogInformation("Pushover personal alert sent to {UserKey}.", handle);
        return true;
    }

    private async Task SendAsync(string appToken, string userKey, int? priorityOverride, AlertNotificationContext context, CancellationToken ct)
    {
        var title = AlertMessageTemplates.PushoverTitle(context);
        var message = AlertMessageTemplates.PushoverBody(context);
        var priority = priorityOverride ?? (context.Severity == AlertSeverity.Critical && !context.IsRecovery ? 1 : 0);

        var payload = new { token = appToken, user = userKey, title, message, priority, timestamp = context.FiredAt.ToUnixTimeSeconds() };

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

    private record PushoverIntegrationConfig([property: Required] string AppToken);
}
