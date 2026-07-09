using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Discord Incoming Webhook.</summary>
public partial class DiscordDispatcher(IHttpClientFactory httpClientFactory, ILogger<DiscordDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Discord;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<DiscordTriggerMeta>(channel.MetaJson);

        var variables = NotificationTemplateHelper.BuildVariables(context);
        string body;
        if (!string.IsNullOrWhiteSpace(meta.Template))
        {
            var content = NotificationTemplateHelper.RenderPlain(meta.Template, variables);
            body = JsonSerializer.Serialize(new
            {
                content,
                username = meta.Username ?? "Piro"
            });
        }
        else
        {
            body = BuildDefaultBody(context, meta.Username);
        }

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
            logger.LogWarning("Discord webhook timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Discord webhook request timed out after 15 seconds.");
        }

        // Discord returns 204 No Content on success
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Discord webhook returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Discord webhook error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Discord alert sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx, string? username)
    {
        var emoji = ctx.IsRecovery ? "✅" : ctx.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var color = ctx.IsRecovery ? 0x22c55e : ctx.Severity switch
        {
            AlertSeverity.Critical => 0xef4444,
            AlertSeverity.Warning  => 0xf59e0b,
            _                      => 0x3b82f6
        };

        var title = ctx.IsRecovery
            ? $"{emoji} RECOVERED — {ctx.ServiceName} / {ctx.CheckName}"
            : $"{emoji} {ctx.Severity.ToString().ToUpperInvariant()} — {ctx.ServiceName} / {ctx.CheckName}";

        var fields = new List<object>
        {
            new { name = "Status", value = ctx.CurrentStatus.ToString(), inline = true }
        };
        if (!ctx.IsRecovery) fields.Add(new { name = "Severity", value = ctx.Severity.ToString(), inline = true });
        if (ctx.AlertDescription is not null) fields.Add(new { name = "Note", value = ctx.AlertDescription, inline = false });

        return JsonSerializer.Serialize(new
        {
            username = username ?? "Piro",
            embeds = new[]
            {
                new
                {
                    title,
                    color,
                    fields,
                    footer = new { text = ctx.FiredAt.ToString("u") }
                }
            }
        });
    }

    private record DiscordTriggerMeta([property: Required] string WebhookUrl, string? Username = null, string? Template = null);
    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

}
