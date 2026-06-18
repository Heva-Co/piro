using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class SiteConfigRepository(PiroDbContext db) : ISiteConfigRepository
{
    private static readonly string[] AllKeys =
    [
        "site:name", "site:url", "site:logo_url", "site:favicon_url",
        "site:meta_title", "site:meta_description", "site:og_image_url",
        "worker:builtin_disabled",
    ];

    public async Task<SiteConfig> GetAsync(CancellationToken ct = default)
    {
        var rows = await db.SiteData
            .Where(s => AllKeys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return new SiteConfig(
            rows.GetValueOrDefault("site:name"),
            rows.GetValueOrDefault("site:url"),
            rows.GetValueOrDefault("site:logo_url"),
            rows.GetValueOrDefault("site:favicon_url"),
            rows.GetValueOrDefault("site:meta_title"),
            rows.GetValueOrDefault("site:meta_description"),
            rows.GetValueOrDefault("site:og_image_url"),
            BuiltinWorkerDisabled: rows.TryGetValue("worker:builtin_disabled", out var flag) &&
                                   string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
        );
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var row = await db.SiteData.FirstOrDefaultAsync(s => s.Key == key, ct);

        if (value is null)
        {
            if (row is not null) db.SiteData.Remove(row);
        }
        else if (row is null)
        {
            db.SiteData.Add(new SiteData
            {
                Key = key, Value = value, DataType = "string",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
