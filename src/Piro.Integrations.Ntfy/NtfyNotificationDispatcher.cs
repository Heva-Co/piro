using System.Text;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.Ntfy;

/// <summary>
/// Delivers a notification <see cref="Event"/> to an ntfy topic (public ntfy.sh or a self-hosted
/// instance) via a plain HTTP POST with ntfy's Title/Priority/Tags headers. It reaches Piro only
/// through <see cref="IIntegrationHost"/>: it asks the host for an HttpClient and for its own
/// decrypted <see cref="NtfyConfig"/>, and renders the neutral event itself — the title/body
/// wording that used to live in the <c>ntfy_title</c>/<c>ntfy_body</c> Scriban templates is ported
/// inline here. It references no Piro.Domain type, no repository, no secret store (RFC 0016 §4.2b).
/// </summary>
public sealed class NtfyNotificationDispatcher : IIntegrationEventHandler, IVerificationCodeSender
{
    private const string DefaultServer = "https://ntfy.sh";

    public string IntegrationId => "Ntfy";

    /// <summary>Posts a one-time verification code to the topic as plain text.</summary>
    public async Task<bool> SendCodeAsync(Guid? integrationId, string handle, string code, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || integrationId is not { } id)
            return false;

        var config = await host.GetConfigAsync<NtfyConfig>(id, ct);
        var serverUrl = string.IsNullOrWhiteSpace(config?.ServerUrl) ? DefaultServer : config.ServerUrl;
        var url = $"{serverUrl.TrimEnd('/')}/{handle}";

        var client = host.GetRequiredService<HttpClient>();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(code, Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Title", "Piro verification code");
        if (!string.IsNullOrWhiteSpace(config?.Token))
            request.Headers.Add("Authorization", $"Bearer {config.Token}");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException) { throw new InvalidOperationException("ntfy request timed out."); }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.Target) || ctx.IntegrationInstanceId is not { } integrationId)
            return false;

        var config = await host.GetConfigAsync<NtfyConfig>(integrationId, ct);
        var serverUrl = string.IsNullOrWhiteSpace(config?.ServerUrl) ? DefaultServer : config.ServerUrl;
        var url = $"{serverUrl.TrimEnd('/')}/{ctx.Target}";

        var isRecovery = evt.IsResolvedLike();
        var title = RenderTitle(evt, isRecovery);
        var body = RenderBody(evt, isRecovery);
        var priority = isRecovery ? 3 : evt.Severity switch
        {
            EventSeverity.Critical => 5,
            EventSeverity.Warning => 4,
            _ => 3,
        };

        var client = host.GetRequiredService<HttpClient>();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Title", EncodeRfc2047(title));
        request.Headers.Add("Priority", priority.ToString());
        request.Headers.Add("Tags", isRecovery ? "white_check_mark" : "rotating_light");
        if (!string.IsNullOrWhiteSpace(config?.Token))
            request.Headers.Add("Authorization", $"Bearer {config.Token}");

        HttpResponseMessage response;
        try { response = await client.SendAsync(request, ct); }
        catch (TaskCanceledException) { throw new InvalidOperationException("ntfy request timed out."); }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"ntfy error {(int)response.StatusCode}: {errorBody}");
        }

        return true;
    }

    /// <summary>Ported from the <c>ntfy_title</c> Scriban template.</summary>
    private static string RenderTitle(Event evt, bool isRecovery) =>
        isRecovery
            ? $"RECOVERED — {evt.Title}"
            : $"{evt.Severity.ToString().ToUpperInvariant()} — {evt.Title}";

    /// <summary>Ported from the <c>ntfy_body</c> Scriban template.</summary>
    private static string RenderBody(Event evt, bool isRecovery)
    {
        var currentStatus = (evt as AlertEvent)?.CurrentStatus ?? string.Empty;

        var sb = new StringBuilder();
        if (isRecovery)
        {
            sb.Append($"{evt.Title} has recovered. Status: {currentStatus}.");
        }
        else
        {
            sb.Append($"{evt.Title} is {currentStatus}.");
            if (evt is AlertEvent { Description: { } description } && !string.IsNullOrWhiteSpace(description))
                sb.Append($" {description}");
        }

        var checkUrl = (evt as AlertEvent)?.CheckUrl;
        if (!string.IsNullOrWhiteSpace(checkUrl))
            sb.Append($" {checkUrl}");

        return sb.ToString();
    }

    /// <summary>RFC 2047 encoding allows non-ASCII characters in the ntfy Title HTTP header.</summary>
    private static string EncodeRfc2047(string value) =>
        value.Any(c => c > 127)
            ? $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?="
            : value;
}

internal static class EventExtensions
{
    /// <summary>True for the "resolved/recovered" event subtypes, so a dispatcher can show a recovery state without an IsRecovery flag.</summary>
    public static bool IsResolvedLike(this Event evt) => evt is AlertResolvedEvent or IncidentResolvedEvent;
}
