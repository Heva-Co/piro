using System.Net;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Domain.Entities;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Sends alert and incident notifications, and verification codes, via SMTP. Email is the one
/// integration that stays in Piro.Infrastructure (its transport is core infrastructure shared with
/// account setup / password reset, RFC 0016 §4.1). It implements the RFC 0016
/// <see cref="INotificationDispatcher"/> like every other integration, plus the core
/// <see cref="IVerificationCodeSender"/>.
/// </summary>
public class EmailDispatcher(
    IEmailService emailService,
    ILogger<EmailDispatcher> logger)
    : INotificationDispatcher, IVerificationCodeSender
{
    public string IntegrationId => "Email";

    public async Task<bool> SendAsync(Event evt, NotificationDelivery delivery, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(delivery.Target)) return false;
        var (subject, body) = Render(evt);
        await emailService.SendAsync(delivery.Target, subject, body, ct);
        logger.LogInformation("Email notification sent to {To}.", delivery.Target);
        return true;
    }

    private static (string Subject, string Body) Render(Event evt)
    {
        switch (evt)
        {
            case IncidentEvent incident:
            {
                var verb = incident is IncidentResolvedEvent ? "resolved" : "opened";
                var services = incident.AffectedServices.Count > 0 ? string.Join(", ", incident.AffectedServices) : "—";
                return ($"[Incident {verb}] {evt.Title}",
                    $"<p><strong>{WebUtility.HtmlEncode(evt.Title)}</strong> — {WebUtility.HtmlEncode(incident.Status)}</p>" +
                    $"<p>Affected services: {WebUtility.HtmlEncode(services)}</p>");
            }
            case AlertEvent alert:
            {
                var state = evt is AlertResolvedEvent ? "Resolved" : evt.Severity.ToString();
                var subject = $"[{state}] {evt.Title}";
                var body = $"<p><strong>{WebUtility.HtmlEncode(evt.Title)}</strong> — {WebUtility.HtmlEncode(state)}</p>";
                if (!string.IsNullOrWhiteSpace(alert.Description))
                    body += $"<p>{WebUtility.HtmlEncode(alert.Description)}</p>";
                if (alert.Url is { } url)
                    body += $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">View in Piro</a></p>";
                return (subject, body);
            }
            default:
                return (evt.Title, $"<p>{WebUtility.HtmlEncode(evt.Title)}</p>");
        }
    }

    public async Task<bool> SendCodeAsync(Integration? integration, string handle, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, "Your Piro verification code", $"<p>{WebUtility.HtmlEncode(code)}</p>", ct);
        logger.LogInformation("Email verification message sent to {To}.", handle);
        return true;
    }
}
