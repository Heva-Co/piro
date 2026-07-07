using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications via the Telegram Bot API (sendMessage).</summary>
public partial class TelegramNotificationChannelDispatcher(IHttpClientFactory httpClientFactory, ILogger<TelegramNotificationChannelDispatcher> logger)
    : INotificationChannelDispatcher
{
    public IntegrationType Type => IntegrationType.Telegram;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<TelegramTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Telegram trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.BotToken))
        {
            logger.LogWarning("Telegram channel {ChannelId} has no bot token configured.", channel.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(meta.ChatId))
        {
            logger.LogWarning("Telegram channel {ChannelId} has no chat ID configured.", channel.Id);
            return;
        }

        var variables = BuildVariables(context);
        var text = string.IsNullOrWhiteSpace(meta.Template)
            ? BuildMessage(context)
            : ReplaceMustache(meta.Template, variables);
        var payload = JsonSerializer.Serialize(new
        {
            chat_id = meta.ChatId,
            text,
            parse_mode = "Markdown"
        });

        var url = $"https://api.telegram.org/bot{meta.BotToken}/sendMessage";

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Telegram request timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Telegram request timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telegram API returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Telegram API error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Telegram alert sent to chat {ChatId} for {Service}/{Check}.",
            meta.ChatId, context.ServiceName, context.CheckName);
    }

    private static string BuildMessage(AlertNotificationContext ctx)
    {
        if (ctx.IsRecovery)
            return $"✅ *RECOVERED* — {EscapeMd(ctx.ServiceName)} / {EscapeMd(ctx.CheckName)}\n\nStatus: `{ctx.CurrentStatus}`\nTime: {ctx.FiredAt:u}";

        var sb = new StringBuilder();
        sb.AppendLine($"🚨 *{ctx.Severity.ToString().ToUpperInvariant()}* — {EscapeMd(ctx.ServiceName)} / {EscapeMd(ctx.CheckName)}");
        sb.AppendLine();
        sb.AppendLine($"Status: `{ctx.CurrentStatus}`");
        sb.AppendLine($"Severity: {ctx.Severity}");
        if (ctx.AlertDescription is not null)
            sb.AppendLine($"Note: {EscapeMd(ctx.AlertDescription)}");
        sb.Append($"Time: {ctx.FiredAt:u}");
        return sb.ToString();
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

    // Escape Markdown v1 special chars that appear in service/check names
    private static string EscapeMd(string s) =>
        s.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");

    private record TelegramTriggerMeta(string BotToken, string ChatId, string? Template);
}
