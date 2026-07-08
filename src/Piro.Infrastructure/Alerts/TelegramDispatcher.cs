using System.ComponentModel.DataAnnotations;
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
public partial class TelegramDispatcher(IHttpClientFactory httpClientFactory, ILogger<TelegramDispatcher> logger)
    : INotificationDispatcher
{
    private const string ApiBase = "https://api.telegram.org";

    public IntegrationType Type => IntegrationType.Telegram;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<TelegramChannelMeta>(channel.MetaJson);
        var botToken = ResolveBotToken(channel);

        var text = string.IsNullOrWhiteSpace(meta.Template)
            ? BuildMessage(context)
            : NotificationTemplateHelper.RenderPlain(meta.Template, NotificationTemplateHelper.BuildVariables(context));

        await SendMessageAsync(botToken, meta.ChatId, text, ct);
        logger.LogInformation("Telegram alert sent to chat {ChatId} for {Service}/{Check}.",
            meta.ChatId, context.ServiceName, context.CheckName);
    }

    public async Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        TelegramIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TelegramIntegrationConfig>(integration.ConfigJson); }
        catch { return false; }

        await SendMessageAsync(config.BotToken, handle, BuildMessage(context), ct);
        logger.LogInformation("Telegram personal alert sent to chat {ChatId}.", handle);
        return true;
    }

    private static string ResolveBotToken(NotificationChannel channel)
    {
        if (channel.Integration is not null)
        {
            var cfg = JsonUtils.DeserializeAndValidate<TelegramIntegrationConfig>(channel.Integration.ConfigJson);
            return cfg.BotToken;
        }
        
        throw new InvalidOperationException();
    }

    private async Task SendMessageAsync(string botToken, string chatId, string text, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { chat_id = chatId, text, parse_mode = "Markdown" });
        var url = $"{ApiBase}/bot{botToken}/sendMessage";

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Telegram request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Telegram API returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Telegram API error {(int)response.StatusCode}: {body}");
        }
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

    private static string EscapeMd(string s) =>
        s.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");

    // MetaJson when Integration provides credentials: only destination + optional template
    private record TelegramChannelMeta(
        [property: Required] string ChatId,
        string? Template = null);

    private record TelegramIntegrationConfig([property: Required] string BotToken);
}
