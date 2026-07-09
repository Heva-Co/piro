using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Opens and closes Opsgenie alerts via the REST API.</summary>
public class OpsgenieDispatcher(IHttpClientFactory httpClientFactory, ILogger<OpsgenieDispatcher> logger)
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Opsgenie;

    public async Task DispatchAsync(NotificationChannel channel, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = JsonUtils.DeserializeAndValidate<OpsgenieTriggerMeta>(channel.MetaJson);

        var baseUrl = string.Equals(meta.Region, "eu", StringComparison.OrdinalIgnoreCase)
            ? "https://api.eu.opsgenie.com"
            : "https://api.opsgenie.com";

        var client = httpClientFactory.CreateClient("piro-webhook");

        if (context.IsRecovery)
        {
            // Close the alert by alias
            var alias = BuildAlias(context);
            using var closeRequest = new HttpRequestMessage(HttpMethod.Delete,
                $"{baseUrl}/v2/alerts/{Uri.EscapeDataString(alias)}?identifierType=alias");
            closeRequest.Headers.Add("Authorization", $"GenieKey {meta.ApiKey}");

            var closeResp = await client.SendAsync(closeRequest, ct);
            if (!closeResp.IsSuccessStatusCode && closeResp.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await closeResp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Opsgenie close alert returned {Status}: {Body}", (int)closeResp.StatusCode, body);
            }
            else
            {
                logger.LogInformation("Opsgenie alert closed for {Service}/{Check}.", context.ServiceName, context.CheckName);
            }
            return;
        }

        // Create alert
        var payload = new
        {
            message = $"{context.ServiceName} / {context.CheckName} is {context.CurrentStatus}",
            alias = BuildAlias(context),
            description = context.AlertDescription ?? $"Status: {context.CurrentStatus}. Severity: {context.Severity}.",
            priority = meta.Priority ?? "P3",
            tags = new[] { "piro", context.ServiceName, context.CheckName }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/alerts");
        request.Headers.Add("Authorization", $"GenieKey {meta.ApiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Opsgenie request timed out for channel {ChannelId}.", channel.Id);
            throw new InvalidOperationException("Opsgenie request timed out after 15 seconds.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Opsgenie returned {Status}: {Body}", (int)response.StatusCode, responseBody);
            throw new InvalidOperationException($"Opsgenie error {(int)response.StatusCode}: {responseBody}");
        }

        logger.LogInformation("Opsgenie alert created for {Service}/{Check}.", context.ServiceName, context.CheckName);
    }

    private static string BuildAlias(AlertNotificationContext ctx) =>
        $"piro-{ctx.ServiceName}-{ctx.CheckName}"
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('/', '-');

    private record OpsgenieTriggerMeta([property: Required] string ApiKey, string? Region = null, string? Priority = null);
    public Task<bool> DispatchPersonalAsync(Integration integration, string handle, AlertNotificationContext context, CancellationToken ct = default) =>
        Task.FromResult(false);

}
