using System.Text;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using System.Text.Json;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends push notifications via ntfy.sh or a self-hosted ntfy instance.</summary>
public class NtfyTriggerDispatcher(IHttpClientFactory httpClientFactory, ILogger<NtfyTriggerDispatcher> logger)
    : ITriggerDispatcher
{
    public TriggerType Type => TriggerType.Ntfy;

    public async Task DispatchAsync(Trigger trigger, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<NtfyTriggerMeta>(trigger.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid ntfy trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.Topic))
        {
            logger.LogWarning("ntfy trigger {TriggerId} has no topic configured.", trigger.Id);
            return;
        }

        var serverUrl = meta.ServerUrl?.TrimEnd('/') ?? "https://ntfy.sh";
        var url = $"{serverUrl}/{meta.Topic}";

        var emoji = context.IsRecovery ? "✅" : context.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var title = context.IsRecovery
            ? $"RECOVERED — {context.ServiceName} / {context.CheckName}"
            : $"{context.Severity.ToString().ToUpperInvariant()} — {context.ServiceName} / {context.CheckName}";

        var message = context.IsRecovery
            ? $"{context.CheckName} has recovered. Status: {context.CurrentStatus}."
            : $"{context.CheckName} is {context.CurrentStatus}.{(context.AlertDescription is not null ? $" {context.AlertDescription}" : "")}";

        // ntfy priority: 5=urgent, 4=high, 3=default, 2=low, 1=min
        var priority = meta.Priority ?? context.Severity switch
        {
            AlertSeverity.Critical => 5,
            AlertSeverity.Warning  => 4,
            _                      => 3
        };
        if (context.IsRecovery) priority = 3;

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(message, Encoding.UTF8, "text/plain");
        request.Headers.Add("Title", title);
        request.Headers.Add("Priority", priority.ToString());
        request.Headers.Add("Tags", context.IsRecovery ? "white_check_mark" : "rotating_light");

        if (!string.IsNullOrWhiteSpace(meta.AccessToken))
            request.Headers.Add("Authorization", $"Bearer {meta.AccessToken}");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("ntfy request timed out for trigger {TriggerId}.", trigger.Id);
            throw new InvalidOperationException("ntfy request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("ntfy returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"ntfy error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("ntfy notification sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private record NtfyTriggerMeta(string Topic, string? ServerUrl = null, string? AccessToken = null, int? Priority = null);
}
