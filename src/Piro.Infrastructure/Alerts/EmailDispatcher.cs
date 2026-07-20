using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert and incident notifications via SMTP.</summary>
public class EmailDispatcher(
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    ILogger<EmailDispatcher> logger)
    : IPersonalNotificationDispatcher<AlertNotificationContext>,
      IPersonalNotificationDispatcher<IncidentNotificationContext>,
      IVerificationCodeSender
{
    public IntegrationType Type => IntegrationType.Email;

    public async Task<bool> SendAsync(Integration? integration, string handle, AlertNotificationContext content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, AlertMessageTemplates.EmailSubject(content), AlertMessageTemplates.EmailBody(content), ct);
        logger.LogInformation("Email personal alert sent to {To}.", handle);
        return true;
    }

    public async Task<bool> SendAsync(Integration? integration, string handle, IncidentNotificationContext content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        var verb = content.IsResolved ? "resolved" : "opened";
        var subject = $"[Incident {verb}] {content.Title}";
        var services = content.AffectedServices.Count > 0 ? string.Join(", ", content.AffectedServices) : "—";
        var body = $"<p><strong>{content.Title}</strong> — {content.Status}</p><p>Affected services: {services}</p>";
        await emailService.SendAsync(handle, subject, body, ct);
        logger.LogInformation("Email incident notification sent to {To}.", handle);
        return true;
    }

    public async Task<bool> SendCodeAsync(Integration? integration, string handle, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, "Your Piro verification code", $"<p>{code}</p>", ct);
        logger.LogInformation("Email verification message sent to {To}.", handle);
        return true;
    }
}
