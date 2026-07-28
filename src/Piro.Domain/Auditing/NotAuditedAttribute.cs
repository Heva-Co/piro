namespace Piro.Domain.Auditing;

/// <summary>
/// Excludes a property from audit snapshots. Apply to anything secret or merely noisy on an
/// <see cref="IAuditable"/> entity — hashes, tokens, keys.
/// </summary>
/// <remarks>
/// This only covers properties declared on our own entities. Sensitive members inherited from
/// ASP.NET Core Identity (<c>PasswordHash</c>, <c>SecurityStamp</c>, …) cannot be annotated here,
/// so the interceptor also carries a name-based deny list. Both mechanisms are needed.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
