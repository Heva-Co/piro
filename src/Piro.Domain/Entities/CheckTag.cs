using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>
/// Assigns a <see cref="Tag"/> (a key) to a <see cref="Check"/>, carrying this check's value for that key.
/// The composite PK <c>(CheckId, TagId)</c> enforces one value per key per check at the database level.
/// </summary>
public class CheckTag : IAuditable
{
    public int CheckId { get; set; }
    public int TagId { get; set; }

    /// <summary>This check's value for the key. Null for a key-only tag.</summary>
    public string? Value { get; set; }

    public Check Check { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
