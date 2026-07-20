namespace Piro.Domain.Attributes;

/// <summary>
/// Marks a config/input property as long-form <b>Markdown</b> prose that should render as a rich
/// Markdown editor (WYSIWYG emitting Markdown) instead of a plain textarea — see
/// <see cref="Enums.ConfigFieldType.Markdown"/>. Use this (not <see cref="MultilineFieldAttribute"/>)
/// when the value is genuinely Markdown that gets rendered downstream, e.g. a Jira ticket description.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MarkdownFieldAttribute : Attribute;
