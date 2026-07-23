namespace Piro.Contracts;

/// <summary>
/// Marks a property on a config class as source code that should render in a code editor (syntax
/// highlighting, line numbers) instead of a plain textarea — see
/// <see cref="Enums.ConfigFieldType.Code"/>. Used for a Script check's script body (RFC 0010).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CodeFieldAttribute : Attribute;
