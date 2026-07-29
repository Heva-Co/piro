using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>
/// A worker tag a <see cref="Check"/> requires in order to run on a worker (RFC 0008 Part B scheduling).
/// This is deliberately a separate table from <see cref="CheckTag"/>: a required worker tag is a
/// constraint the check places on OTHER entities (workers), not a label describing the check itself, so it
/// must stay out of the check's own effective-tags and service→check inheritance. It reuses the shared
/// <see cref="Tag"/> key catalog (an FK to Tag) so a check requiring, e.g., <c>piro:region=eu</c> references
/// the same vocabulary workers advertise. The composite PK <c>(CheckId, TagId)</c> enforces one required
/// value per key per check. An empty set means "run on any live worker" (today's behavior).
/// </summary>
public class CheckRequiredWorkerTag : IAuditable
{
    public int CheckId { get; set; }
    public int TagId { get; set; }

    /// <summary>The required value for the key. Null means "the worker must carry this key" (any value).</summary>
    public string? Value { get; set; }

    public Check Check { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
