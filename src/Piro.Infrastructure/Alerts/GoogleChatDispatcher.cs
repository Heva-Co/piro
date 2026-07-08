using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Google Chat Incoming Webhook.</summary>
public partial class GoogleChatDispatcher(IHttpClientFactory httpClientFactory, ILogger<GoogleChatDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.GoogleChat;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<GoogleChatTriggerMeta>(channel.MetaJson);

        var variables = NotificationTemplateHelper.BuildVariables(context);
        var body = string.IsNullOrWhiteSpace(meta.Body)
            ? BuildDefaultBody(context)
            : NotificationTemplateHelper.RenderPlain(meta.Body, variables);

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, meta.WebhookUrl);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Google Chat webhook timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Google Chat webhook request timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Chat webhook returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Google Chat webhook error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Google Chat alert sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx)
    {
        var emoji = ctx.IsRecovery ? "✅" : ctx.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var header = ctx.IsRecovery
            ? $"{emoji} *RECOVERED* — {ctx.ServiceName} / {ctx.CheckName}"
            : $"{emoji} *{ctx.Severity.ToString().ToUpperInvariant()}* — {ctx.ServiceName} / {ctx.CheckName}";

        var lines = new List<string> { $"*Status:* {ctx.CurrentStatus}" };
        if (!ctx.IsRecovery) lines.Add($"*Severity:* {ctx.Severity}");
        if (ctx.AlertDescription is not null) lines.Add($"*Note:* {ctx.AlertDescription}");
        lines.Add($"*Time:* {ctx.FiredAt:u}");

        // Google Chat Cards v2 — simple card with header + text paragraph
        return JsonSerializer.Serialize(new
        {
            cardsV2 = new[]
            {
                new
                {
                    cardId = "piro-alert",
                    card = new
                    {
                        header = new { title = header },
                        sections = new[]
                        {
                            new
                            {
                                widgets = new[]
                                {
                                    new { textParagraph = new { text = string.Join("<br>", lines) } }
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    private record GoogleChatTriggerMeta([property: Required] string WebhookUrl, string? Body = null);
    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

}
