using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Trigger"/> entities.</summary>
public interface ITriggerRepository
{
    Task<IEnumerable<Trigger>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Trigger>> GetGlobalAsync(CancellationToken ct = default);
    Task<Trigger?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Trigger> CreateAsync(Trigger trigger, CancellationToken ct = default);
    Task<Trigger> UpdateAsync(Trigger trigger, CancellationToken ct = default);
    Task DeleteAsync(Trigger trigger, CancellationToken ct = default);
}
