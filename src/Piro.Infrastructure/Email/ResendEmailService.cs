using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Email;

/// <summary>
/// Sends transactional emails via the Resend REST API.
/// API reference: https://github.com/resend/resend-openapi/blob/main/resend.yaml
/// Endpoint: POST https://api.resend.com/emails
/// Required fields: from (string), to (string | string[], max 50), subject (string).
/// Auth: Authorization: Bearer {apiKey}
/// </summary>
public class ResendEmailService(
    IEmailConfigRepository emailConfig,
    IHttpClientFactory httpClientFactory,
    ILogger<ResendEmailService> logger) : IEmailService
{
    private const string ResendApiUrl = "https://api.resend.com/emails";

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default, string? from = null)
    {
        var config = await emailConfig.GetAsync(ct);

        var apiKey = config.ResendApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Resend API key is not configured. Skipping email to {To}.", to);
            return;
        }

        from ??= config.ResendFrom;
        if (string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("Resend from address is not configured. Skipping email to {To}.", to);
            return;
        }

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        // `to` accepts string or string[] (max 50) — we always send as array for consistency
        var payload = new { from, to = new[] { to }, subject, html = htmlBody };
        var response = await http.PostAsJsonAsync(ResendApiUrl, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend API returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Resend API error ({(int)response.StatusCode}): {body}");
        }

        logger.LogInformation("Email sent via Resend to {To}: {Subject}.", to, subject);
    }

    public Task SendInvitationAsync(string to, string inviteUrl, CancellationToken ct = default) =>
        SendAsync(to, "You've been invited to Piro", EmailTemplates.Invitation(inviteUrl), ct);
}
