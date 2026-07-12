using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications via the Telegram Bot API (sendMessage).</summary>
public class TelegramDispatcher(IHttpClientFactory httpClientFactory, ILogger<TelegramDispatcher> logger)
    : INotificationDispatcher
{
    private const string ApiBase = "https://api.telegram.org";

    public IntegrationType Type => IntegrationType.Telegram;

    public async Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        TelegramIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TelegramIntegrationConfig>(integration.ConfigJson); }
        catch { return false; }

        await SendMessageAsync(config.BotToken, handle, AlertMessageTemplates.Telegram(context), ct);
        logger.LogInformation("Telegram personal alert sent to chat {ChatId}.", handle);
        return true;
    }

    public async Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integration is null) return false;
        TelegramIntegrationConfig config;
        try { config = JsonUtils.DeserializeAndValidate<TelegramIntegrationConfig>(integration.ConfigJson); }
        catch { return false; }

        await SendMessageAsync(config.BotToken, handle, message, ct);
        logger.LogInformation("Telegram verification message sent to chat {ChatId}.", handle);
        return true;
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

    private record TelegramIntegrationConfig([property: Required] string BotToken);
}
