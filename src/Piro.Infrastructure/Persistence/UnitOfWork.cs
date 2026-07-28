using Microsoft.EntityFrameworkCore.Storage;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Persistence;

/// <summary>
/// A reentrant unit of work: nested <see cref="BeginAsync"/> calls join the outermost transaction
/// rather than opening a second one.
/// </summary>
/// <remarks>
/// Nesting exists so an operation that already wraps itself in a transaction — the check write path —
/// can be composed by a caller that needs several of them to commit together, which is what lets the
/// config reconciler reuse the application services instead of duplicating their write path
/// (RFC 0019 §4.3, §4.12). Only the outermost Begin opens a real transaction and only the outermost
/// Commit commits it; an inner Commit is a no-op. A rollback at any depth marks the whole unit
/// aborted, so an inner failure can never be committed by an outer scope that did not notice it.
/// </remarks>
public sealed class UnitOfWork(PiroDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _tx;
    private int _depth;
    private bool _aborted;

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_depth++ == 0)
        {
            _tx = await db.Database.BeginTransactionAsync(ct);
            _aborted = false;
        }
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_depth == 0) throw new InvalidOperationException("No active transaction.");

        // An inner scope reporting success does not end the transaction — the outermost one decides.
        if (--_depth > 0) return;

        if (_tx is null) throw new InvalidOperationException("No active transaction.");

        if (_aborted)
        {
            // Committing after an inner scope rolled back would silently persist a partial write.
            await DiscardAsync(ct);
            throw new InvalidOperationException(
                "The transaction was rolled back by a nested operation and cannot be committed.");
        }

        try { await _tx.CommitAsync(ct); }
        finally { await DisposeTransactionAsync(); }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_depth == 0 || _tx is null) return;

        _aborted = true;

        // Unwind to the outermost scope before touching the database: an inner rollback must undo the
        // whole unit, but the outer scope is still on the stack and will call Commit or Rollback itself.
        if (--_depth > 0) return;

        await DiscardAsync(ct);
    }

    private async Task DiscardAsync(CancellationToken ct)
    {
        if (_tx is null) return;
        try { await _tx.RollbackAsync(ct); }
        finally { await DisposeTransactionAsync(); }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_tx is not null) await _tx.DisposeAsync();
        _tx = null;
        _depth = 0;
        _aborted = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx is not null)
            await _tx.DisposeAsync();
    }
}
