using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="AlertConfig"/> entities.</summary>
public interface IAlertConfigRepository
{
    Task<IEnumerable<AlertConfig>> GetByCheckIdAsync(int checkId, CancellationToken ct = default);
    Task<IEnumerable<AlertConfig>> GetAllAsync(CancellationToken ct = default);
    Task<AlertConfig?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AlertConfig> CreateAsync(AlertConfig config, CancellationToken ct = default);
    Task<AlertConfig> UpdateAsync(AlertConfig config, CancellationToken ct = default);
    Task DeleteAsync(AlertConfig config, CancellationToken ct = default);
}
