using System.Text;
using System.Text.Json;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Telegram;

/// <summary>
/// Delivers a notification <see cref="Event"/> to a Telegram chat via the Bot API (sendMessage). It
/// reaches Piro only through <see cref="IIntegrationHost"/>: it asks the host for an HttpClient and
/// for its own decrypted <see cref="TelegramConfig"/>, and renders the neutral event itself. It
/// references no Piro.Domain type, no repository, no secret store — the boundary is the assembly's
/// reference graph plus the host window (RFC 0016 §4.2b).
/// </summary>
public sealed class TelegramNotificationDispatcher : INotificationDispatcher
{
    private const string ApiBase = "https://api.telegram.org";

    public string IntegrationId => "Telegram";

    public async Task<bool> SendAsync(Event evt, NotificationDelivery delivery, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(delivery.Target) || delivery.IntegrationId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<TelegramConfig>(integrationId, ct);
        if (config is null || string.IsNullOrWhiteSpace(config.BotToken))
            return false;

        await SendMessageAsync(host, config.BotToken, delivery.Target, Render(evt), ct);
        return true;
    }

    private static string Render(Event evt)
    {
        var status = evt.IsResolvedLike() ? "✅ Resolved" : $"⚠️ {evt.Severity}";
        var sb = new StringBuilder();
        sb.AppendLine($"*{Escape(evt.Title)}*");
        sb.AppendLine(status);
        if (evt is AlertEvent { Description: { } d } && !string.IsNullOrWhiteSpace(d))
            sb.AppendLine(Escape(d));
        if (evt.FiredAtDisplay is { } when) sb.AppendLine(when);
        if (evt.Url is { } url) sb.AppendLine(url);
        return sb.ToString().TrimEnd();
    }

    private async Task SendMessageAsync(IIntegrationHost host, string botToken, string chatId, string text, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { chat_id = chatId, text, parse_mode = "Markdown" });
        var client = host.GetRequiredService<HttpClient>();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/bot{botToken}/sendMessage")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException) { throw new InvalidOperationException("Telegram request timed out."); }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Telegram API error {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>Telegram legacy Markdown parse_mode requires escaping these characters in interpolated text.</summary>
    private static string Escape(string s)
    {
        foreach (var c in new[] { "_", "*", "`", "[" })
            s = s.Replace(c, "\\" + c);
        return s;
    }
}

internal static class EventExtensions
{
    /// <summary>True for the "resolved/recovered" event subtypes, so a dispatcher can show a recovery state without an IsRecovery flag.</summary>
    public static bool IsResolvedLike(this Event evt) => evt is AlertResolvedEvent or IncidentResolvedEvent;
}
