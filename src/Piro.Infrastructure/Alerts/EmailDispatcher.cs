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
    : INotificationDispatcher
{
    public IntegrationType Type => IntegrationType.Email;

    public async Task<bool> DispatchPersonalAsync(Integration? integration, string handle, AlertNotificationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, AlertMessageTemplates.EmailSubject(context), AlertMessageTemplates.EmailBody(context), ct);
        logger.LogInformation("Email personal alert sent to {To}.", handle);
        return true;
    }

    public async Task<bool> SendPersonalMessageAsync(Integration? integration, string handle, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, "Your Piro verification code", $"<p>{message}</p>", ct);
        logger.LogInformation("Email verification message sent to {To}.", handle);
        return true;
    }
}
