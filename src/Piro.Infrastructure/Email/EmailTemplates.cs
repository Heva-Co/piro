using System.Reflection;
using Scriban;

namespace Piro.Infrastructure.Email;

/// <summary>
/// Compiles and renders the transactional email templates embedded under Email/Templates/. HTML lives
/// in .scriban files, not interpolated in C#, so Piro.Application never needs to know how an email is
/// rendered.
/// </summary>
public static class EmailTemplates
{
    private static readonly Dictionary<string, Template> Compiled = new();

    static EmailTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.Contains(".Email.Templates.", StringComparison.Ordinal)) continue;
            if (!name.EndsWith(".scriban", StringComparison.Ordinal)) continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            var template = Template.Parse(source, name);
            if (template.HasErrors)
                throw new InvalidOperationException(
                    $"Email template '{name}' failed to parse: {string.Join("; ", template.Messages)}");

            var key = name.Split('.')[^2];
            Compiled[key] = template;
        }
    }

    public static string Invitation(string inviteUrl) =>
        Render("invitation", new { invite_url = inviteUrl });

    public static string PasswordReset(string resetUrl) =>
        Render("reset-password", new { reset_url = resetUrl });

    /// <summary>The branded one-time verification-code email (setup / email-config verification).</summary>
    public static string VerificationCode(string code, int minutes) =>
        Render("verification-code", new { code, minutes });

    /// <summary>
    /// The branded alert notification email. All string fields are interpolated raw into HTML, so the
    /// caller must HTML-encode any user-supplied value (check/service/description/…) before passing it —
    /// Scriban does not auto-escape. Optional fields may be null and are omitted by the template.
    /// </summary>
    public static string Alert(AlertEmailModel model) => Render("alert", model);

    private static string Render(string templateKey, object model)
    {
        if (!Compiled.TryGetValue(templateKey, out var template))
            throw new InvalidOperationException($"No email template embedded for '{templateKey}'.");

        return template.Render(model);
    }
}

/// <summary>
/// View model for the alert email template. Field names are snake_case to match the Scriban
/// placeholders. All string values are rendered raw into HTML — the caller HTML-encodes user-supplied
/// text before constructing this. Optional fields are null when absent and the template omits them.
/// </summary>
public sealed record AlertEmailModel
{
    public required string status { get; init; }
    public required string severity_bg { get; init; }
    public required string severity_fg { get; init; }
    public required string check { get; init; }
    public string? service { get; init; }
    public string? description { get; init; }
    public string? current_status { get; init; }
    public string? value { get; init; }
    public string? source { get; init; }
    public string? fired_at { get; init; }
    public string? url { get; init; }
}
