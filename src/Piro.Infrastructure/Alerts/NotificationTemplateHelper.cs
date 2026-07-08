using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Configuration;
using Piro.Application.Interfaces;
using Piro.Application.Models;

namespace Piro.Infrastructure.Alerts;

/// <summary>
/// Shared template variable building and Mustache rendering used by all alert dispatchers.
/// </summary>
internal static partial class NotificationTemplateHelper
{
    /// <summary>Basic variables available without async I/O (no site config).</summary>
    public static Dictionary<string, string> BuildVariables(AlertNotificationContext ctx) => new()
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
        ["alert_cta_text"]          = ctx.IsRecovery ? "View Status Page" : "View Incident",
        ["is_resolved"]             = ctx.IsRecovery ? "true" : "false",
        ["is_triggered"]            = ctx.IsRecovery ? "false" : "true",
        ["colors_down"]             = "#dc2626",
        ["colors_up"]               = "#16a34a",
    };

    /// <summary>Full variables including site config (requires async I/O).</summary>
    public static async Task<Dictionary<string, string>> BuildVariablesAsync(
        AlertNotificationContext ctx,
        ISiteConfigRepository siteConfigRepo,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var siteCfg  = await siteConfigRepo.GetAsync(ct);
        var siteUrl  = siteCfg.Url  ?? configuration["PublicUrl"] ?? string.Empty;
        var siteName = siteCfg.Name ?? configuration["SiteName"]  ?? "Piro";
        var logoUrl  = siteCfg.LogoUrl is not null
                       ? siteUrl.TrimEnd('/') + siteCfg.LogoUrl
                       : string.Empty;

        var vars = BuildVariables(ctx);
        vars["alert_cta_url"]   = siteUrl;
        vars["site_url"]        = siteUrl;
        vars["site_name"]       = siteName;
        vars["site_logo_url"]   = logoUrl;
        return vars;
    }

    /// <summary>
    /// Renders a Mustache template supporting:
    /// - {{variable}} — HTML-escaped substitution
    /// - {{{variable}}} — raw (unescaped) substitution
    /// - {{#variable}}...{{/variable}} — include block if variable is non-empty and not "false"
    /// </summary>
    public static string Render(string template, Dictionary<string, string> vars)
    {
        template = SectionPattern().Replace(template, m =>
        {
            var key     = m.Groups[1].Value.Trim();
            var content = m.Groups[2].Value;
            var truthy  = vars.TryGetValue(key, out var val)
                          && !string.IsNullOrEmpty(val)
                          && val != "false";
            return truthy ? content : string.Empty;
        });

        template = TripleMustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? val : string.Empty;
        });

        template = DoubleMustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? HttpUtility.HtmlEncode(val) : m.Value;
        });

        return template;
    }

    /// <summary>Renders a simple (non-HTML) Mustache template — no escaping.</summary>
    public static string RenderPlain(string template, Dictionary<string, string> vars) =>
        SimpleMustachePattern().Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            return vars.TryGetValue(key, out var val) ? val : m.Value;
        });

    [GeneratedRegex(@"\{\{#(\w+)\}\}(.*?)\{\{/\1\}\}", RegexOptions.Singleline)]
    private static partial Regex SectionPattern();

    [GeneratedRegex(@"\{\{\{(\w+)\}\}\}")]
    private static partial Regex TripleMustachePattern();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex DoubleMustachePattern();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex SimpleMustachePattern();
}
