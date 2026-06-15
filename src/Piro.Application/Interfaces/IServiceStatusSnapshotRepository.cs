using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence contract for <see cref="ServiceStatusSnapshot"/> time-series records.</summary>
public interface IServiceStatusSnapshotRepository
{
    Task UpsertAsync(ServiceStatusSnapshot snapshot, CancellationToken ct = default);
    Task<IEnumerable<ServiceStatusSnapshot>> GetByServiceIdAsync(int serviceId, long? from = null, long? to = null, CancellationToken ct = default);
    Task<IEnumerable<(long DayTimestamp, int CountUp, int CountDown, int CountDegraded, int CountMaintenance)>> GetDailyCountsAsync(int serviceId, long from, long to, CancellationToken ct = default);
}
