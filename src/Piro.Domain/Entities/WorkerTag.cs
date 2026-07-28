using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>
/// Assigns a <see cref="Tag"/> (a key) to a <see cref="WorkerRegistration"/>, carrying this worker's value
/// for that key. The composite PK <c>(WorkerRegistrationId, TagId)</c> enforces one value per key per
/// worker at the database level. Worker tags do not inherit from anything; a worker has no parent.
/// </summary>
public class WorkerTag : IAuditable
{
    public Guid WorkerRegistrationId { get; set; }
    public int TagId { get; set; }

    /// <summary>This worker's value for the key. Null for a key-only tag.</summary>
    public string? Value { get; set; }

    public WorkerRegistration Worker { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
