using Microsoft.EntityFrameworkCore.Storage;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Persistence;

public sealed class UnitOfWork(PiroDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _tx;

    public async Task BeginAsync(CancellationToken ct = default)
        => _tx = await db.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_tx is null) throw new InvalidOperationException("No active transaction.");
        await _tx.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_tx is not null)
            await _tx.RollbackAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx is not null)
            await _tx.DisposeAsync();
    }
}
