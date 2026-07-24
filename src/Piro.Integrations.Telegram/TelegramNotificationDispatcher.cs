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
public sealed class TelegramNotificationDispatcher : IIntegrationEventHandler, IVerificationCodeSender
{
    private const string ApiBase = "https://api.telegram.org";

    public string IntegrationId => "Telegram";

    public async Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.Target) || ctx.IntegrationInstanceId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<TelegramConfig>(integrationId, ct);
        if (config is null || string.IsNullOrWhiteSpace(config.BotToken))
            return false;

        await SendMessageAsync(host, config.BotToken, ctx.Target, Render(evt, host), ct);
        return true;
    }

    /// <summary>Sends a one-time verification code to a chat as plain text — same transport, no template.</summary>
    public async Task<bool> SendCodeAsync(Guid? integrationId, string handle, string code, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integrationId is not { } id)
            return false;

        var config = await host.GetConfigAsync<TelegramConfig>(id, ct);
        if (config is null || string.IsNullOrWhiteSpace(config.BotToken))
            return false;

        await SendMessageAsync(host, config.BotToken, handle, code, ct);
        return true;
    }

    // The Telegram message body lives as a Scriban template embedded in this assembly (Templates/
    // notification.scriban), rendered through the host's parser (RFC 0016) rather than concatenated in
    // C#. The model carries the already-escaped/derived fields (legacy-Markdown escaping and the
    // resolved-vs-severity status can't be expressed in the template).
    private static readonly string MessageTemplate = LoadTemplate("notification.scriban");

    private static string Render(Event evt, IIntegrationHost host)
    {
        var parser = host.GetRequiredService<ITemplateParser>();
        var model = new
        {
            title = Escape(evt.Title),
            status = evt.IsResolvedLike() ? "✅ Resolved" : $"⚠️ {evt.Severity}",
            description = evt is AlertEvent { Description: { } d } && !string.IsNullOrWhiteSpace(d) ? Escape(d) : null,
            fired_at = evt.FiredAtDisplay,
            url = evt.Url,
        };
        return parser.Render(MessageTemplate, model).TrimEnd();
    }

    private static string LoadTemplate(string fileName)
    {
        var assembly = typeof(TelegramNotificationDispatcher).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".Templates.{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
