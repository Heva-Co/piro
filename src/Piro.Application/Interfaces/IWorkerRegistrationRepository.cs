using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="WorkerRegistration"/> entities.</summary>
public interface IWorkerRegistrationRepository
{
    Task<WorkerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkerRegistration?> FindByWorkerTokenHashAsync(string hash, CancellationToken ct = default);
    Task<IEnumerable<WorkerRegistration>> GetAllAsync(CancellationToken ct = default);
    Task<WorkerRegistration> CreateAsync(WorkerRegistration worker, CancellationToken ct = default);
    Task<WorkerRegistration> UpdateAsync(WorkerRegistration worker, CancellationToken ct = default);
    Task DeleteAsync(WorkerRegistration worker, CancellationToken ct = default);
    Task ClearDefaultAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically clears IsDefault on every worker and sets it on <paramref name="worker"/>,
    /// in a single transaction — prevents two concurrent "set as default" operations from
    /// both observing zero existing defaults and leaving two workers marked default.
    /// </summary>
    Task SetAsDefaultAsync(WorkerRegistration worker, CancellationToken ct = default);
}
