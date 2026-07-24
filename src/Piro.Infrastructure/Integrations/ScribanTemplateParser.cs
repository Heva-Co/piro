using System.Collections.Concurrent;
using Piro.Integrations.Abstractions;
using Scriban;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// The concrete <see cref="ITemplateParser"/> (RFC 0016) — backs the integration-facing template seam
/// with Scriban, the engine Piro already uses for its own email/alert templates. Integrations author
/// their notification bodies as Scriban templates and render them here without referencing or pinning
/// Scriban themselves. Compiled templates are cached by source, since an integration renders the same
/// (small, constant) template on every notification.
/// </summary>
internal sealed class ScribanTemplateParser : ITemplateParser
{
    private readonly ConcurrentDictionary<string, Template> _compiled = new();

    public string Render(string template, object model)
    {
        var compiled = _compiled.GetOrAdd(template, static source =>
        {
            var parsed = Template.Parse(source);
            if (parsed.HasErrors)
                throw new InvalidOperationException(
                    $"Template failed to parse: {string.Join("; ", parsed.Messages)}");
            return parsed;
        });

        return compiled.Render(model);
    }
}
