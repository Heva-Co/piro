namespace Piro.Integrations.Abstractions;

/// <summary>
/// Renders a text template against a model (RFC 0016) — the seam that lets an integration author its
/// notification bodies as templates instead of concatenating strings. Resolved from the host
/// (<c>host.GetRequiredService&lt;ITemplateParser&gt;()</c>); Piro backs it with its template engine
/// (Scriban) so the integration neither references nor pins that dependency.
/// </summary>
public interface ITemplateParser
{
    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="model"/>, resolving
    /// <c>{{ property }}</c> placeholders from the model's members (e.g. the neutral event). Returns the
    /// rendered text; a template error surfaces as an exception for the integration to handle.
    /// </summary>
    string Render(string template, object model);
}
