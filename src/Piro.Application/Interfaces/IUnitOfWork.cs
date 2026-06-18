namespace Piro.Application.Interfaces;

/// <summary>
/// Wraps a database transaction so multiple repository operations can be committed or
/// rolled back atomically without leaking infrastructure concerns into the application layer.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
