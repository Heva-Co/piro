namespace Piro.Contracts;

/// <summary>
/// Marks a config/input field whose select options are <b>fetched at runtime</b> from the connected
/// integration, rather than a fixed set (contrast <see cref="ConfigFieldOptionsAttribute"/>) — e.g. the
/// Jira projects/issue types of the connected account (RFC 0012). The field carries a
/// <see cref="SourceKey"/>; an IOptionsProvider registered for (IntegrationType, SourceKey) resolves the
/// list on demand, and the admin form renders a select populated from a generic options endpoint. This
/// keeps dynamic population generic — any integration adds a provider + this attribute, with no
/// per-provider code in the schema engine or the form.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DynamicOptionsAttribute(string sourceKey) : Attribute
{
    /// <summary>Stable key identifying the options source, resolved to an IOptionsProvider (e.g. "jira-projects").</summary>
    public string SourceKey { get; } = sourceKey;

    /// <summary>
    /// Optional sibling field (by property name) this field's options depend on — a cascade. When set,
    /// the form re-fetches options as that field changes and passes its value to the provider (e.g. Jira
    /// issue types depend on the chosen project). Null for an independent source.
    /// </summary>
    public string? DependsOn { get; init; }
}
