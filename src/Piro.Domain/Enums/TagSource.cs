namespace Piro.Domain.Enums;

/// <summary>
/// Who owns a tag key. Fixed by the key's namespace: any key in the <c>piro:</c> namespace is
/// <see cref="System"/>, every other key is <see cref="User"/>. A property of the key, not of the
/// assignment, so it lives on <see cref="Piro.Domain.Entities.Tag"/> rather than the join.
/// </summary>
public enum TagSource
{
    User,
    System
}
