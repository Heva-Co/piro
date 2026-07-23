namespace Piro.Contracts;

/// <summary>
/// Display metadata for a property on an Integration config class (see
/// <see cref="IntegrationManifestAttribute"/>), used to render a schema-driven admin form without
/// hand-written per-type components. Standard Data Annotations (<see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/>,
/// <see cref="System.ComponentModel.DataAnnotations.UrlAttribute"/>, <see cref="System.ComponentModel.DataAnnotations.EmailAddressAttribute"/>)
/// still drive validation and <see cref="Enums.ConfigFieldType"/> inference — this attribute only
/// adds the human-facing label/placeholder/help text, which Data Annotations has no concept of.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigFieldAttribute(string label) : Attribute
{
    /// <summary>
    /// Human-readable field label shown above the input (e.g. "Client ID" for
    /// <c>JiraConfig.ClientId</c>). Falls back to the property name when this attribute is absent.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>Example value shown as the input's placeholder (e.g. "https://your-org.atlassian.net").</summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// Short help text rendered below the input — setup instructions, a link to generate a
    /// credential, format notes, etc. (e.g. "Generate one at id.atlassian.com/...").
    /// </summary>
    public string? HelpText { get; init; }
}
