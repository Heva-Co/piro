namespace Piro.Domain.Entities;

/// <summary>
/// Assigns a <see cref="Tag"/> (a key) to a <see cref="Service"/>, carrying this service's value for that
/// key. The composite PK <c>(ServiceId, TagId)</c> enforces one value per key per service at the database
/// level, so a service can never hold two rows for the same key.
/// </summary>
public class ServiceTag
{
    public int ServiceId { get; set; }
    public int TagId { get; set; }

    /// <summary>This service's value for the key. Null for a key-only tag (e.g. <c>critical</c>).</summary>
    public string? Value { get; set; }

    public Service Service { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
