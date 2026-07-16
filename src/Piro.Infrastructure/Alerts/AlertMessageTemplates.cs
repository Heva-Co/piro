using System.Net;
using System.Reflection;
using Piro.Application.Models;
using Piro.Domain.Enums;
using Scriban;
using Scriban.Runtime;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Single source of truth for every dispatcher's alert message body. Each channel's exact wording
/// (Markdown for Telegram, HTML for Email, plain text for SMS/Pushover/ntfy) lives in its own
/// Scriban template under Alerts/Templates/, embedded as a resource and compiled once at startup —
/// centralizing what used to be scattered as string-interpolation in each dispatcher's own
/// BuildMessage/BuildDefaultBody method.
/// </summary>
internal static class AlertMessageTemplates
{
    private static readonly Dictionary<string, Template> Compiled = new();

    static AlertMessageTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.EndsWith(".scriban", StringComparison.Ordinal)) continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            var template = Template.Parse(source, name);
            if (template.HasErrors)
                throw new InvalidOperationException(
                    $"Alert message template '{name}' failed to parse: {string.Join("; ", template.Messages)}");

            // Resource name is "Piro.Infrastructure.Alerts.Templates.telegram.scriban" — key on the file stem.
            var key = name.Split('.')[^2];
            Compiled[key] = template;
        }
    }

    /// <summary>Telegram uses legacy Markdown parse_mode — service/check names must have Markdown special chars escaped.</summary>
    public static string Telegram(AlertNotificationContext ctx) => Render("telegram", ctx with
    {
        ServiceName = EscapeTelegramMarkdown(ctx.ServiceName),
        CheckName = EscapeTelegramMarkdown(ctx.CheckName),
        AlertDescription = ctx.AlertDescription is not null ? EscapeTelegramMarkdown(ctx.AlertDescription) : null,
    });

    private static string EscapeTelegramMarkdown(string s) =>
        s.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");

    public static string EmailSubject(AlertNotificationContext ctx) => Render("email_subject", ctx);

    /// <summary>Body is raw HTML — service/check names and the alert description must be HTML-encoded to prevent markup injection.</summary>
    public static string EmailBody(AlertNotificationContext ctx) => Render("email_body", ctx with
    {
        ServiceName = WebUtility.HtmlEncode(ctx.ServiceName),
        CheckName = WebUtility.HtmlEncode(ctx.CheckName),
        AlertDescription = ctx.AlertDescription is not null ? WebUtility.HtmlEncode(ctx.AlertDescription) : null,
    });
    public static string TwilioSms(AlertNotificationContext ctx) => Render("twilio_sms", ctx);
    public static string PushoverTitle(AlertNotificationContext ctx) => Render("pushover_title", ctx);
    public static string PushoverBody(AlertNotificationContext ctx) => Render("pushover_body", ctx);
    public static string NtfyTitle(AlertNotificationContext ctx) => Render("ntfy_title", ctx);
    public static string NtfyBody(AlertNotificationContext ctx) => Render("ntfy_body", ctx);

    private static string Render(string templateKey, AlertNotificationContext ctx)
    {
        if (!Compiled.TryGetValue(templateKey, out var template))
            throw new InvalidOperationException($"No alert message template embedded for '{templateKey}'.");

        var scriptObject = new ScriptObject();
        scriptObject.Import(BuildModel(ctx));

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        return template.Render(context).Trim();
    }

    private static object BuildModel(AlertNotificationContext ctx) => new
    {
        title = ctx.Title(),
        service_name = ctx.ServiceName,
        check_name = ctx.CheckName,
        service_url = ctx.ServiceUrl,
        check_url = ctx.CheckUrl,
        alert_url = ctx.AlertUrl,
        current_status = ctx.CurrentStatus.ToString(),
        severity = ctx.Severity.ToString().ToUpperInvariant(),
        alert_description = ctx.AlertDescription,
        is_recovery = ctx.IsRecovery,
        is_external = ctx.IsExternal,
        source_label = ctx.SourceLabel,
        source_url = ctx.SourceUrl,
        // Pre-formatted in the recipient's own time zone (see AlertNotificationContext.FiredAtDisplay) —
        // never derive display time from FiredAt directly, it's still the raw UTC instant.
        fired_at = ctx.FiredAtDisplay ?? ctx.FiredAt.ToString("u"),
        severity_emoji = ctx.IsRecovery ? "✅" : ctx.Severity switch
        {
            AlertSeverity.Critical => "🔴",
            AlertSeverity.Warning => "🟡",
            _ => "🔵",
        },
    };
}
