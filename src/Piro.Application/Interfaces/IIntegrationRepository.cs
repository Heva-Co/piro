using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IIntegrationRepository
{
    Task<IEnumerable<Integration>> GetAllAsync(CancellationToken ct = default);
    Task<Integration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Integration> CreateAsync(Integration integration, CancellationToken ct = default);
    Task<Integration> UpdateAsync(Integration integration, CancellationToken ct = default);
    Task DeleteAsync(Integration integration, CancellationToken ct = default);
}
