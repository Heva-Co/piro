using System.Net;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Contracts;
using Piro.Infrastructure.Email;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Sends alert and incident notifications, and verification codes, via SMTP. Email is the one
/// integration that stays in Piro.Infrastructure (its transport is core infrastructure shared with
/// account setup / password reset, RFC 0016 §4.1). It implements the RFC 0016
/// <see cref="IIntegrationEventHandler"/> like every other integration, plus the
/// <see cref="IVerificationCodeSender"/> — as a platform-wide channel it has no per-instance config, so
/// it ignores the integration id and host and sends through the shared email service.
/// </summary>
public class EmailDispatcher(
    IEmailService emailService,
    ILogger<EmailDispatcher> logger)
    : IIntegrationEventHandler, IVerificationCodeSender
{
    public string IntegrationId => "Email";

    public async Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ctx.Target)) return false;
        var (subject, body) = Render(evt);
        await emailService.SendAsync(ctx.Target, subject, body, ct);
        logger.LogInformation("Email notification sent to {To}.", ctx.Target);
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
                var resolved = evt is AlertResolvedEvent;
                var state = resolved ? "Resolved" : evt.Severity.ToString();
                var subject = $"[{state}] {evt.Title}";
                var (bg, fg) = SeverityColors(resolved, evt.Severity);
                // Scriban does not auto-escape, so every user-supplied field is HTML-encoded here.
                var model = new AlertEmailModel
                {
                    status = state,
                    severity_bg = bg,
                    severity_fg = fg,
                    check = WebUtility.HtmlEncode(alert.CheckName),
                    service = Encode(alert.ServiceName),
                    description = Encode(alert.Description),
                    current_status = Encode(alert.CurrentStatus),
                    value = Encode(alert.Value),
                    source = alert.IsExternal ? Encode(alert.SourceLabel) : null,
                    fired_at = Encode(evt.FiredAtDisplay),
                    url = alert.Url is { } u ? WebUtility.HtmlEncode(u) : null,
                };
                return (subject, EmailTemplates.Alert(model));
            }
            default:
                return (evt.Title, $"<p>{WebUtility.HtmlEncode(evt.Title)}</p>");
        }
    }

    /// <summary>HTML-encodes an optional field, returning null when it's null/blank so the template omits it.</summary>
    private static string? Encode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlEncode(value);

    /// <summary>Background/foreground for the severity badge — a resolved alert reads green regardless of its original severity.</summary>
    private static (string Bg, string Fg) SeverityColors(bool resolved, EventSeverity severity)
    {
        if (resolved) return ("#dcfce7", "#15803d");            // green-100 / green-700
        return severity switch
        {
            EventSeverity.Critical => ("#fee2e2", "#b91c1c"),   // red-100 / red-700
            EventSeverity.Warning => ("#fef9c3", "#a16207"),    // yellow-100 / yellow-700
            _ => ("#dbeafe", "#1d4ed8"),                         // blue-100 / blue-700
        };
    }

    public async Task<bool> SendCodeAsync(Guid? integrationId, string handle, string code, IIntegrationHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handle)) return false;
        await emailService.SendAsync(handle, "Your Piro verification code", $"<p>{WebUtility.HtmlEncode(code)}</p>", ct);
        logger.LogInformation("Email verification message sent to {To}.", handle);
        return true;
    }
}
