namespace Piro.Contracts;

/// <summary>
/// Names a rich client-side validator for a config field (RFC 0011) — e.g.
/// <c>[ConfigValidation("statusCodes")]</c> or <c>[ConfigValidation("ipOrHostname")]</c>. The name
/// is emitted on the field's schema (ConfigFieldSchemaDto.Validator) and resolved by the admin
/// against a validator registry, so a format rule lives next to the field it guards, in one place,
/// instead of a hand-written per-type branch. Orthogonal to <c>[Required]</c> (presence) — this is
/// about the shape of a non-empty value. The backend still enforces the real rule on write; this is
/// the client-side mirror for inline errors.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigValidationAttribute(string validator) : Attribute
{
    /// <summary>The registry key of the validator to apply (e.g. "statusCodes", "ipOrHostname", "dnsExpectedValue").</summary>
    public string Validator { get; } = validator;
}
