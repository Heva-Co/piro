using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Service"/> aggregate.</summary>
public interface IServiceRepository
{
    Task<IEnumerable<Service>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Returns a dictionary of service ID → check count for all services.</summary>
    Task<Dictionary<int, int>> GetCheckCountsAsync(CancellationToken ct = default);
    Task<Service?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Service?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<Service> CreateAsync(Service service, CancellationToken ct = default);
    Task<Service> UpdateAsync(Service service, CancellationToken ct = default);
    Task DeleteAsync(Service service, CancellationToken ct = default);

    /// <summary>Returns the direct BLOCKING dependency service IDs for cycle detection traversal.</summary>
    Task<IEnumerable<int>> GetBlockingDependencyIdsAsync(int serviceId, CancellationToken ct = default);
}
