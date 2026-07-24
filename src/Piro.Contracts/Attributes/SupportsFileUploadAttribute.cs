namespace Piro.Contracts;

/// <summary>
/// Marks a property on an Integration config class (typically a <see cref="MultilineFieldAttribute"/>
/// field holding pasted JSON, like <c>GoogleCloudConfig.ServiceAccountJson</c>) as also acceptable
/// via file upload in the admin form. Explicit and independent of
/// <see cref="Enums.ConfigFieldType.Multiline"/> — not every multiline field is necessarily a file
/// upload candidate, so the frontend must not infer this from field type alone.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SupportsFileUploadAttribute : Attribute;
