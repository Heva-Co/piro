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

    private static string Render(string templateKey, object model)
    {
        if (!Compiled.TryGetValue(templateKey, out var template))
            throw new InvalidOperationException($"No email template embedded for '{templateKey}'.");

        return template.Render(model);
    }
}
