using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Google Chat Incoming Webhook.</summary>
public partial class GoogleChatTriggerDispatcher(IHttpClientFactory httpClientFactory, ILogger<GoogleChatTriggerDispatcher> logger)
    : ITriggerDispatcher
{
    public TriggerType Type => TriggerType.GoogleChat;

    public async Task DispatchAsync(Trigger trigger, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<GoogleChatTriggerMeta>(trigger.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Google Chat trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.WebhookUrl))
        {
            logger.LogWarning("Google Chat trigger {TriggerId} has no webhook URL configured.", trigger.Id);
            return;
        }

        var variables = BuildVariables(context);
        var body = string.IsNullOrWhiteSpace(meta.Body)
            ? BuildDefaultBody(context)
            : ReplaceMustache(meta.Body, variables);

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
            logger.LogWarning("Google Chat webhook timed out for trigger {TriggerId}.", trigger.Id);
            throw new InvalidOperationException("Google Chat webhook request timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Chat webhook returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Google Chat webhook error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Google Chat alert sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx)
    {
        var emoji = ctx.IsRecovery ? "✅" : ctx.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var header = ctx.IsRecovery
            ? $"{emoji} *RECOVERED* — {ctx.ServiceName} / {ctx.CheckName}"
            : $"{emoji} *{ctx.Severity.ToString().ToUpperInvariant()}* — {ctx.ServiceName} / {ctx.CheckName}";

        var lines = new List<string> { $"*Status:* {ctx.CurrentStatus}" };
        if (!ctx.IsRecovery) lines.Add($"*Severity:* {ctx.Severity}");
        if (ctx.AlertDescription is not null) lines.Add($"*Note:* {ctx.AlertDescription}");
        lines.Add($"*Time:* {ctx.FiredAt:u}");

        // Google Chat Cards v2 — simple card with header + text paragraph
        return JsonSerializer.Serialize(new
        {
            cardsV2 = new[]
            {
                new
                {
                    cardId = "piro-alert",
                    card = new
                    {
                        header = new { title = header },
                        sections = new[]
                        {
                            new
                            {
                                widgets = new[]
                                {
                                    new { textParagraph = new { text = string.Join("<br>", lines) } }
                                }
                            }
                        }
                    }
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

    private record GoogleChatTriggerMeta(string WebhookUrl, string? Body = null);
}
