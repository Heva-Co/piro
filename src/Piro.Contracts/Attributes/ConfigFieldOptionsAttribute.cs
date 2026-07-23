namespace Piro.Contracts;

/// <summary>
/// Marks a property on an Integration config class as a fixed set of valid values (e.g.
/// <c>OpsgenieConfig.Region</c>: "US"/"EU") — the presence of this attribute is what makes a field
/// render as <see cref="Enums.ConfigFieldType.Enum"/> (a select) instead of a free-text input.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigFieldOptionsAttribute(params string[] options) : Attribute
{
    /// <summary>The valid values for this field, in display order.</summary>
    public string[] Options { get; } = options;
}
