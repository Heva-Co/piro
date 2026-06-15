using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>
/// Dispatches a check to an executor (local or remote) and arranges for result ingestion once execution completes.
/// </summary>
public interface ICheckJobDispatcher
{
    Task DispatchAsync(Check check, CancellationToken ct = default);
}
