namespace Piro.Contracts;

/// <summary>
/// Marks a property on an Integration config class as long-form text (e.g. a pasted JSON key
/// file) that should render as a multi-line textarea instead of a single-line input — see
/// <see cref="Enums.ConfigFieldType.Multiline"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MultilineFieldAttribute : Attribute;
