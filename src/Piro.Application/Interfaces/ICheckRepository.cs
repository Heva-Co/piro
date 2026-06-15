using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Check"/> entities.</summary>
public interface ICheckRepository
{
    Task<IEnumerable<Check>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default);
    Task<IEnumerable<Check>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<Check>> GetAllWithServiceAsync(CancellationToken ct = default);
    Task<IEnumerable<(Check Check, string? LastErrorMessage)>> GetAllWithServiceAndLastErrorAsync(CancellationToken ct = default);
    Task<Check?> GetBySlugAsync(int serviceId, string slug, CancellationToken ct = default);
    Task<Check?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> SlugExistsInServiceAsync(int serviceId, string slug, CancellationToken ct = default);
    Task<Check> CreateAsync(Check check, CancellationToken ct = default);
    Task<Check> UpdateAsync(Check check, CancellationToken ct = default);
    Task DeleteAsync(Check check, CancellationToken ct = default);
}
