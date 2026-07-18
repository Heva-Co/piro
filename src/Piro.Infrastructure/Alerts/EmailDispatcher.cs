using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications via SMTP.</summary>
public class EmailDispatcher(
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    ILogger<EmailDispatcher> logger)
    : IPersonalNotificationDispatcher<AlertNotificationContext>, IVerificationCodeSender
{
    public IntegrationType Type => IntegrationType.Email;

    public async Task<bool> SendAsync(Integration? integration, string handle, AlertNotificationContext content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, AlertMessageTemplates.EmailSubject(content), AlertMessageTemplates.EmailBody(content), ct);
        logger.LogInformation("Email personal alert sent to {To}.", handle);
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
