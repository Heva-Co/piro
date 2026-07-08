using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>POSTs a JSON payload to a webhook URL when an alert fires or recovers.</summary>
public partial class WebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Webhook;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<WebhookTriggerMeta>(channel.MetaJson);

        var variables = NotificationTemplateHelper.BuildVariables(context);
        string body = string.IsNullOrWhiteSpace(meta.Body)
            ? BuildDefaultBody(context)
            : NotificationTemplateHelper.RenderPlain(meta.Body, variables);

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, meta.Url);
        request.Content = new StringContent(body, Encoding.UTF8);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        if (!string.IsNullOrWhiteSpace(meta.Secret))
        {
            var signature = ComputeHmacSha256(body, meta.Secret);
            request.Headers.TryAddWithoutValidation("X-Piro-Signature", $"sha256={signature}");
        }

        // Apply custom headers
        if (meta.Headers is not null)
        {
            foreach (var h in meta.Headers)
            {
                if (!string.IsNullOrWhiteSpace(h.Key) && !string.IsNullOrWhiteSpace(h.Value))
                    request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        logger.LogInformation("Webhook POST {Url} | Content-Type: {CT} | Body: {Body}",
            meta.Url, request.Content.Headers.ContentType, body);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Webhook {Url} timed out for channel {ChannelId}.", meta.Url, channel.Id);
            throw new InvalidOperationException($"Webhook request to {meta.Url} timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Webhook {Url} returned {Status}: {ResponseBody}", meta.Url, (int)response.StatusCode, responseBody);
        else
            logger.LogInformation("Webhook dispatched to {Url} — response: {ResponseBody}", meta.Url, responseBody);
    }

    private static string BuildDefaultBody(AlertNotificationContext context) => JsonSerializer.Serialize(new
    {
        event_type = context.IsRecovery ? "alert.recovered" : "alert.fired",
        service = context.ServiceName,
        check = context.CheckName,
        status = context.CurrentStatus.ToString(),
        severity = context.Severity.ToString(),
        description = context.AlertDescription,
        fired_at = context.FiredAt
    });

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private record WebhookHeader(string Key, string Value);
    private record WebhookTriggerMeta([property: Required] string Url, string? Secret = null, string? Body = null, List<WebhookHeader>? Headers = null);
    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

}
