using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Discord Incoming Webhook.</summary>
public partial class DiscordNotificationChannelDispatcher(IHttpClientFactory httpClientFactory, ILogger<DiscordNotificationChannelDispatcher> logger)
    : INotificationChannelDispatcher
{
    public NotificationChannelType Type => NotificationChannelType.Discord;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<DiscordTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Discord trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.WebhookUrl))
        {
            logger.LogWarning("Discord channel {ChannelId} has no webhook URL configured.", channel.Id);
            return;
        }

        var variables = BuildVariables(context);
        string body;
        if (!string.IsNullOrWhiteSpace(meta.Template))
        {
            var content = ReplaceMustache(meta.Template, variables);
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

    private static Dictionary<string, string> BuildVariables(AlertNotificationContext ctx) => new()
    {
        ["alert_name"]        = ctx.CheckName,
        ["alert_for"]         = ctx.ServiceName,
        ["alert_status"]      = ctx.CurrentStatus.ToString(),
        ["alert_severity"]    = ctx.Severity.ToString(),
        ["alert_description"] = ctx.AlertDescription ?? string.Empty,
        ["alert_timestamp"]   = ctx.FiredAt.ToString("O"),
        ["is_resolved"]       = ctx.IsRecovery ? "true" : "false",
        ["is_triggered"]      = ctx.IsRecovery ? "false" : "true",
    };

    private static string ReplaceMustache(string template, Dictionary<string, string> vars) =>
        MustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? val : m.Value;
        });

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex MustachePattern();

    private record DiscordTriggerMeta(string WebhookUrl, string? Username = null, string? Template = null);
}
