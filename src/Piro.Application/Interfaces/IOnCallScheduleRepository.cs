using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IOnCallScheduleRepository
{
    Task<(IEnumerable<OnCallSchedule> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<List<OnCallScheduleMembersDto>> GetAllWithMembersAsync(CancellationToken ct = default);
    Task<OnCallSchedule?> GetByIdWithLayersAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns only the schedules a user actually appears in — as a rotation member on any layer,
    /// or as the target/replaced user on any override — with layers, layer users, and overrides
    /// loaded so each can be expanded. Backs the user's personal on-call calendar.
    /// </summary>
    Task<List<OnCallSchedule>> GetSchedulesForUserAsync(int userId, CancellationToken ct = default);
    Task<OnCallSchedule> CreateAsync(OnCallSchedule schedule, CancellationToken ct = default);
    Task UpdateAsync(OnCallSchedule schedule, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<OnCallLayer> CreateLayerAsync(OnCallLayer layer, CancellationToken ct = default);
    Task<OnCallLayer> UpdateLayerAsync(OnCallLayer layer, CancellationToken ct = default);
    Task DeleteLayerAsync(int layerId, CancellationToken ct = default);
    Task<OnCallOverride> CreateOverrideAsync(OnCallOverride ov, CancellationToken ct = default);
    Task DeleteOverrideAsync(int overrideId, CancellationToken ct = default);
}
