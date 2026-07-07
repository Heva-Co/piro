using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Microsoft Teams Incoming Webhook using Adaptive Cards.</summary>
public partial class MsTeamsNotificationChannelDispatcher(IHttpClientFactory httpClientFactory, ILogger<MsTeamsNotificationChannelDispatcher> logger)
    : INotificationChannelDispatcher
{
    public IntegrationType Type => IntegrationType.MSTeams;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<MsTeamsTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Microsoft Teams trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.Url))
        {
            logger.LogWarning("Microsoft Teams channel {ChannelId} has no webhook URL configured.", channel.Id);
            return;
        }

        var variables = BuildVariables(context);
        var body = string.IsNullOrWhiteSpace(meta.Body)
            ? BuildDefaultBody(context)
            : ReplaceMustache(meta.Body, variables);

        var client = httpClientFactory.CreateClient("piro-webhook");
        using var request = new HttpRequestMessage(HttpMethod.Post, meta.Url);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Microsoft Teams webhook timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Microsoft Teams webhook request timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Microsoft Teams webhook returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Microsoft Teams webhook error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Microsoft Teams alert sent for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx)
    {
        var color = ctx.IsRecovery ? "Good" : ctx.Severity switch
        {
            AlertSeverity.Critical => "Attention",
            AlertSeverity.Warning  => "Warning",
            _                      => "Accent"
        };

        var title = ctx.IsRecovery
            ? $"✅ RECOVERED — {ctx.ServiceName} / {ctx.CheckName}"
            : $"🔴 {ctx.Severity.ToString().ToUpperInvariant()} — {ctx.ServiceName} / {ctx.CheckName}";

        var facts = new List<object>
        {
            new { title = "Status", value = ctx.CurrentStatus.ToString() }
        };
        if (!ctx.IsRecovery) facts.Add(new { title = "Severity", value = ctx.Severity.ToString() });
        if (ctx.AlertDescription is not null) facts.Add(new { title = "Note", value = ctx.AlertDescription });
        facts.Add(new { title = "Time", value = ctx.FiredAt.ToString("u") });

        // Adaptive Card v1.4
        return JsonSerializer.Serialize(new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = title,
                                weight = "Bolder",
                                size = "Medium",
                                color
                            },
                            new
                            {
                                type = "FactSet",
                                facts
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

    private record MsTeamsTriggerMeta(string Url, string? Body = null);
}
