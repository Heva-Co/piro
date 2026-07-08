using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Posts an alert notification to a Microsoft Teams Incoming Webhook using Adaptive Cards.</summary>
public partial class MsTeamsDispatcher(IHttpClientFactory httpClientFactory, ILogger<MsTeamsDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.MSTeams;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<MsTeamsTriggerMeta>(channel.MetaJson);

        var variables = NotificationTemplateHelper.BuildVariables(context);
        var body = string.IsNullOrWhiteSpace(meta.Body)
            ? BuildDefaultBody(context)
            : NotificationTemplateHelper.RenderPlain(meta.Body, variables);

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

    private record MsTeamsTriggerMeta([property: Required] string Url, string? Body = null);
    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

}
