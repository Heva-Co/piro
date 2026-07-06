using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Slack Incoming Webhook URL.</summary>
public partial class SlackNotificationChannelDispatcher(IHttpClientFactory httpClientFactory, ILogger<SlackNotificationChannelDispatcher> logger)
    : INotificationChannelDispatcher
{
    public IntegrationType Type => IntegrationType.Slack;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Deserialize<SlackTriggerMeta>(channel.MetaJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid Slack trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.Url))
        {
            logger.LogWarning("Slack channel {ChannelId} has no webhook URL configured.", channel.Id);
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
            logger.LogWarning("Slack webhook timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Slack webhook request timed out after 15 seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Slack webhook {Url} returned {Status}: {Body}", meta.Url, (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Slack webhook error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Slack alert sent to webhook for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx)
    {
        var emoji = ctx.IsRecovery ? "✅" : ctx.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning  => "🟡",
            _                      => "🔵"
        };

        var headerText = ctx.IsRecovery
            ? $"{emoji} *RECOVERED* — {ctx.ServiceName} / {ctx.CheckName}"
            : $"{emoji} *{ctx.Severity.ToString().ToUpperInvariant()}* — {ctx.ServiceName} / {ctx.CheckName}";

        var bodyLines = new List<string> { $"Status: *{ctx.CurrentStatus}*" };
        if (!ctx.IsRecovery) bodyLines.Add($"Severity: {ctx.Severity}");
        if (ctx.AlertDescription is not null) bodyLines.Add($"Note: {ctx.AlertDescription}");
        bodyLines.Add($"Time: {ctx.FiredAt:u}");

        return JsonSerializer.Serialize(new
        {
            blocks = new object[]
            {
                new { type = "section", text = new { type = "mrkdwn", text = headerText } },
                new { type = "section", text = new { type = "mrkdwn", text = string.Join("\n", bodyLines) } },
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

    private record SlackTriggerMeta(string Url, string? Body = null);
}
