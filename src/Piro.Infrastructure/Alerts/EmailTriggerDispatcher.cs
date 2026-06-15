using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Alerts;

/// <summary>Sends alert notifications via SMTP using MailKit.</summary>
public partial class EmailTriggerDispatcher(
    IEmailService emailService,
    IConfiguration configuration,
    ISiteConfigRepository siteConfigRepo,
    ILogger<EmailTriggerDispatcher> logger)
    : ITriggerDispatcher
{
    public TriggerType Type => TriggerType.Email;

    public async Task DispatchAsync(Trigger trigger, AlertNotificationContext context, CancellationToken ct = default)
    {
        var meta = System.Text.Json.JsonSerializer.Deserialize<EmailTriggerMeta>(trigger.MetaJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid email trigger metadata.");

        if (string.IsNullOrWhiteSpace(meta.To))
        {
            logger.LogWarning("Email trigger {TriggerId} has no recipient address configured.", trigger.Id);
            return;
        }

        var subject = context.IsRecovery
            ? $"[RECOVERED] {context.ServiceName} / {context.CheckName}"
            : $"[{context.Severity.ToString().ToUpperInvariant()}] {context.ServiceName} / {context.CheckName}";

        string body;
        if (!string.IsNullOrWhiteSpace(meta.Template))
        {
            var vars = await BuildVariablesAsync(context, ct);
            body = RenderMustache(meta.Template, vars);
        }
        else
        {
            body = BuildDefaultBody(context);
        }

        var from = string.IsNullOrWhiteSpace(meta.From) ? null : meta.From;
        await emailService.SendAsync(meta.To, subject, body, ct, from);

        logger.LogInformation("Email alert sent to {To} for {Service}/{Check}.", meta.To, context.ServiceName, context.CheckName);
    }

    private async Task<Dictionary<string, string>> BuildVariablesAsync(AlertNotificationContext ctx, CancellationToken ct)
    {
        var siteCfg  = await siteConfigRepo.GetAsync(ct);
        var siteUrl  = siteCfg.Url  ?? configuration["PublicUrl"] ?? string.Empty;
        var siteName = siteCfg.Name ?? configuration["SiteName"]  ?? "Piro";
        var logoUrl  = siteCfg.LogoUrl is not null
                       ? siteUrl.TrimEnd('/') + siteCfg.LogoUrl
                       : string.Empty;

        return new Dictionary<string, string>
        {
            ["alert_id"]                = ctx.CheckId.ToString(),
            ["alert_name"]              = ctx.CheckName,
            ["alert_for"]               = ctx.ServiceName,
            ["alert_status"]            = ctx.CurrentStatus.ToString(),
            ["alert_severity"]          = ctx.Severity.ToString(),
            ["alert_description"]       = ctx.AlertDescription ?? string.Empty,
            ["alert_message"]           = ctx.AlertDescription ?? string.Empty,
            ["alert_timestamp"]         = ctx.FiredAt.ToString("O"),
            ["alert_value"]             = ctx.AlertValue ?? string.Empty,
            ["alert_failure_threshold"] = ctx.FailureThreshold.ToString(),
            ["alert_success_threshold"] = ctx.SuccessThreshold.ToString(),
            ["alert_incident_url"]      = ctx.IncidentUrl ?? string.Empty,
            ["alert_cta_url"]           = siteUrl,
            ["alert_cta_text"]          = ctx.IsRecovery ? "View Status Page" : "View Incident",
            ["is_resolved"]             = ctx.IsRecovery ? "true" : "false",
            ["is_triggered"]            = ctx.IsRecovery ? "false" : "true",
            ["site_url"]                = siteUrl,
            ["site_name"]               = siteName,
            ["site_logo_url"]           = logoUrl,
            ["colors_down"]             = "#dc2626",
            ["colors_up"]               = "#16a34a",
        };
    }

    /// <summary>
    /// Renders a Mustache template, supporting:
    /// - {{variable}} — HTML-escaped substitution
    /// - {{{variable}}} — raw (unescaped) substitution
    /// - {{#variable}}...{{/variable}} — include block if variable is non-empty and not "false"
    /// </summary>
    private static string RenderMustache(string template, Dictionary<string, string> vars)
    {
        // Sections first: {{#var}}...{{/var}}
        template = SectionPattern().Replace(template, m =>
        {
            var key     = m.Groups[1].Value.Trim();
            var content = m.Groups[2].Value;
            var truthy  = vars.TryGetValue(key, out var val)
                          && !string.IsNullOrEmpty(val)
                          && val != "false";
            return truthy ? content : string.Empty;
        });

        // Triple braces — unescaped (must run before double-brace pass)
        template = TripleMustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? val : string.Empty;
        });

        // Double braces — HTML-escaped
        template = DoubleMustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? HttpUtility.HtmlEncode(val) : m.Value;
        });

        return template;
    }

    private static string BuildDefaultBody(AlertNotificationContext ctx) =>
        ctx.IsRecovery
            ? $"<p>✅ <strong>{ctx.CheckName}</strong> on <strong>{ctx.ServiceName}</strong> has recovered.</p>" +
              $"<p>Status: {ctx.CurrentStatus}<br>Time: {ctx.FiredAt:u}</p>"
            : $"<p>🚨 Alert fired for <strong>{ctx.CheckName}</strong> on <strong>{ctx.ServiceName}</strong>.</p>" +
              $"<p>Status: {ctx.CurrentStatus}<br>Severity: {ctx.Severity}" +
              $"{(ctx.AlertDescription is not null ? $"<br>Note: {ctx.AlertDescription}" : string.Empty)}<br>Time: {ctx.FiredAt:u}</p>";

    [GeneratedRegex(@"\{\{#(\w+)\}\}(.*?)\{\{/\1\}\}", RegexOptions.Singleline)]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"\{\{\{(\w+)\}\}\}")]
    private static partial Regex TripleMustachePattern();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex DoubleMustachePattern();

    private record EmailTriggerMeta(string To, string? From = null, string? Template = null);
}
