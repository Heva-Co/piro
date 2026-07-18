namespace Piro.Domain.Attributes;

/// <summary>
/// Marks a config property as conditionally visible: the admin form shows it only when the sibling
/// field named <see cref="Field"/> currently holds one of <see cref="Values"/> (RFC 0011). For
/// example, an HTTP check's Body is <c>[VisibleWhen("method", "POST", "PUT", "PATCH")]</c> — hidden
/// for GET/HEAD. A property without this attribute is always visible. The referenced field name is
/// the camelCase config key (as it appears in ConfigJson), matching <see cref="ConfigFieldOptionsAttribute"/> peers.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VisibleWhenAttribute(string field, params string[] values) : Attribute
{
    /// <summary>The camelCase key of the sibling field whose value gates this one.</summary>
    public string Field { get; } = field;

    /// <summary>The values of <see cref="Field"/> for which this field is shown.</summary>
    public string[] Values { get; } = values;
}
