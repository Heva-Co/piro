using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>Reads scheduling status (next/previous fire time, trigger state) of all registered Quartz jobs.</summary>
public interface IJobStatusService
{
    Task<IEnumerable<JobStatusDto>> GetAllAsync(CancellationToken ct = default);
}
