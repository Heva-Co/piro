using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IOnCallScheduleRepository
{
    Task<List<OnCallSchedule>> GetAllAsync(CancellationToken ct = default);
    Task<OnCallSchedule?> GetByIdWithLayersAsync(int id, CancellationToken ct = default);
    Task<OnCallSchedule> CreateAsync(OnCallSchedule schedule, CancellationToken ct = default);
    Task UpdateAsync(OnCallSchedule schedule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<OnCallLayer> CreateLayerAsync(OnCallLayer layer, CancellationToken ct = default);
    Task<OnCallLayer> UpdateLayerAsync(OnCallLayer layer, CancellationToken ct = default);
    Task DeleteLayerAsync(int layerId, CancellationToken ct = default);
    Task<OnCallOverride> CreateOverrideAsync(OnCallOverride ov, CancellationToken ct = default);
    Task DeleteOverrideAsync(int overrideId, CancellationToken ct = default);
}
