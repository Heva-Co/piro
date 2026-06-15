using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

public interface ILogRepository
{
    Task<LogPageDto> GetPagedAsync(LogQueryParams query, CancellationToken ct = default);
    Task PruneAsync(DateTime olderThan, CancellationToken ct = default);
}
