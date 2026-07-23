using System.Text;
using System.Text.Json;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.GoogleChat;

/// <summary>
/// Posts alert and incident notifications to a Google Chat space via its incoming-webhook URL
/// (RFC 0009 mode 2). The whole space is the audience, so this is a channel dispatcher — there is no
/// per-person handle, and <see cref="NotificationDelivery.Target"/> is unused (the webhook URL is
/// already space-specific).
/// <para>
/// It reaches Piro only through <see cref="IIntegrationHost"/>: it asks the host for an HttpClient and
/// for its own decrypted <see cref="GoogleChatConfig"/>, and renders the neutral <see cref="Event"/>
/// itself. It references no Piro.Domain type, no repository, no secret store (RFC 0016 §4.2b).
/// </para>
/// </summary>
public sealed class GoogleChatNotificationDispatcher : INotificationDispatcher
{
    public string IntegrationId => "GoogleChat";

    public async Task<bool> SendAsync(Event evt, NotificationDelivery delivery, IIntegrationHost host, CancellationToken ct = default)
    {
        if (delivery.IntegrationId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<GoogleChatConfig>(integrationId, ct);
        if (config is null || string.IsNullOrWhiteSpace(config.WebhookUrl))
            return false;

        var text = Render(evt);
        if (text is null)
            return false;

        return await PostAsync(host, config.WebhookUrl, text, ct);
    }

    private static string? Render(Event evt) => evt switch
    {
        AlertEvent alert => BuildAlertMessage(alert),
        IncidentEvent incident => BuildIncidentMessage(incident),
        _ => null,
    };

    private static string BuildAlertMessage(AlertEvent evt)
    {
        var recovered = evt is AlertResolvedEvent;
        var icon = recovered ? "✅" : evt.Severity == EventSeverity.Critical ? "🔴" : "⚠️";
        var verb = recovered ? "recovered" : "fired";
        var line = $"{icon} *{evt.Title}* {verb}";
        return evt.Value is { Length: > 0 } value ? $"{line}\n{value}" : line;
    }

    private static string BuildIncidentMessage(IncidentEvent evt)
    {
        var resolved = evt is IncidentResolvedEvent;
        var icon = resolved ? "✅" : "🔴";
        var verb = resolved ? "resolved" : "opened";
        var line = $"{icon} *Incident {verb}:* {evt.Title} ({evt.Status})";
        return evt.AffectedServices.Count > 0 ? $"{line}\nAffected: {string.Join(", ", evt.AffectedServices)}" : line;
    }

    // Google Chat's simplest supported payload: a plain-text message to the space the webhook targets.
    private async Task<bool> PostAsync(IIntegrationHost host, string webhookUrl, string text, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { text });

        var client = host.GetRequiredService<HttpClient>();
        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
            return false;

        return true;
    }
}
