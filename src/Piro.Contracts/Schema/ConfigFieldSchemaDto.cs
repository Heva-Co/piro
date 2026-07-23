namespace Piro.Contracts;

/// <summary>
/// Wire representation of a single config property, reflected from a config type's Data Annotations
/// by <see cref="Extensions.ConfigSchemaBuilder"/> — never hand-authored. Shared by both Integration
/// manifests (see IntegrationManifestAttribute) and Check manifests (see CheckTypeManifestAttribute,
/// RFC 0011).
/// </summary>
public record ConfigFieldSchemaDto(
    string Key,
    string Label,
    ConfigFieldType Type,
    bool Required,
    bool IsSecret,
    bool SupportsFileUpload,
    string? Placeholder,
    string? HelpText,
    IReadOnlyList<string>? Options,
    /// <summary>True for a field the server generates itself (e.g. a webhook auth token) — see GeneratedFieldAttribute. The admin form never renders an input for it before creation.</summary>
    bool IsGenerated,
    /// <summary>
    /// The field's default value, reflected from the config record's property initializer
    /// (e.g. Method → "GET", TimeoutMs → 5000, Port → 443) so the admin can seed a new form from the
    /// schema alone (RFC 0011). Null when the property has no non-default initializer.
    /// </summary>
    object? Default = null,
    /// <summary>
    /// For an <see cref="ConfigFieldType.ObjectArray"/> field only: the reflected field schema of
    /// the list element type, rendered recursively as a repeater of sub-forms (e.g. an HTTP check's
    /// ResponseRules — RFC 0011). Null for every scalar field type.
    /// </summary>
    IReadOnlyList<ConfigFieldSchemaDto>? ItemSchema = null,
    /// <summary>
    /// Conditional visibility (RFC 0011): when set, the admin form shows this field only if the
    /// sibling field <see cref="ConfigFieldVisibilityDto.Field"/> holds one of
    /// <see cref="ConfigFieldVisibilityDto.Values"/> (e.g. an HTTP Body shown only for POST/PUT/PATCH).
    /// Null means always visible.
    /// </summary>
    ConfigFieldVisibilityDto? VisibleWhen = null,
    /// <summary>
    /// Registry key of a rich client-side validator for this field (RFC 0011) — see
    /// ConfigValidationAttribute. Null when the field has only presence/type validation. The admin
    /// resolves it against its validator registry to produce inline errors.
    /// </summary>
    string? Validator = null,
    /// <summary>
    /// Source key of a runtime-populated options list (RFC 0012) — see DynamicOptionsAttribute. When
    /// set, the form fetches the field's select options from
    /// <c>GET /integrations/{id}/options/{optionsSource}</c> instead of using static <see cref="Options"/>.
    /// Null for a static/free-text field.
    /// </summary>
    string? OptionsSource = null,
    /// <summary>
    /// Sibling field name this field's dynamic options depend on — a cascade (e.g. Jira issue types
    /// depend on the chosen project). The form re-fetches when it changes and passes its value along.
    /// Null when the source is independent. Only meaningful with <see cref="OptionsSource"/>.
    /// </summary>
    string? OptionsDependsOn = null
);

/// <summary>A conditional-visibility rule for a config field — see ConfigFieldSchemaDto.VisibleWhen.</summary>
public record ConfigFieldVisibilityDto(
    string Field,
    IReadOnlyList<string> Values
);
