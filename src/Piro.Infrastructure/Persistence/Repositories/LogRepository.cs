using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class LogRepository(PiroDbContext db) : ILogRepository
{
    public async Task<LogPageDto> GetPagedAsync(LogQueryParams query, CancellationToken ct = default)
    {
        var q = db.PiroLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Level))
            q = q.Where(l => l.Level == query.Level);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(l => l.Message.Contains(query.Search) ||
                              (l.SourceContext != null && l.SourceContext.Contains(query.Search)) ||
                              (l.Exception != null && l.Exception.Contains(query.Search)));

        if (query.From.HasValue)
            q = q.Where(l => l.Timestamp >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(l => l.Timestamp <= query.To.Value);

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(1, query.Page);

        var items = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogDto(l.Id, l.Timestamp, l.Level, l.Message, l.Exception, l.SourceContext, l.Properties))
            .ToListAsync(ct);

        return new LogPageDto(items, total, page, pageSize);
    }

    public async Task PruneAsync(DateTime olderThan, CancellationToken ct = default)
    {
        await db.PiroLogs
            .Where(l => l.Timestamp < olderThan)
            .ExecuteDeleteAsync(ct);
    }
}
