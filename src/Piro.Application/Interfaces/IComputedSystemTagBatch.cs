namespace Piro.Application.Interfaces;

/// <summary>
/// A computed (on-read) system tag (RFC 0008 §4.2): a <c>piro:*</c> tag derived from external, self-changing
/// state (an open incident, an active alert) and never stored, so it is always fresh and never needs a
/// sweep. Batch-first so the effective-tags read path costs one query per tag, not N+1.
/// </summary>
/// <typeparam name="TEntity">The tagged entity (only <see cref="Piro.Domain.Entities.Service"/> today).</typeparam>
public interface IComputedSystemTagBatch<TEntity>
{
    /// <summary>The <c>piro:*</c> key this batch computes.</summary>
    string Key { get; }

    /// <summary>Returns the subset of the given entity ids the tag applies to, in one query.</summary>
    Task<ISet<int>> ComputeForAsync(IReadOnlyCollection<int> entityIds, CancellationToken ct);
}
