using Microsoft.EntityFrameworkCore;
using Piro.Application.Constants;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class SiteConfigRepository(PiroDbContext db) : ISiteConfigRepository
{
    public async Task<SiteConfig> GetAsync(CancellationToken ct = default)
    {
        var rows = await db.SiteData
            .Where(s => SiteDataKeys.All.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return new SiteConfig(
            rows.GetValueOrDefault(SiteDataKeys.SiteName),
            rows.GetValueOrDefault(SiteDataKeys.SiteUrl),
            rows.GetValueOrDefault(SiteDataKeys.SiteLogoUrl),
            rows.GetValueOrDefault(SiteDataKeys.SiteFaviconUrl),
            rows.GetValueOrDefault(SiteDataKeys.SiteMetaTitle),
            rows.GetValueOrDefault(SiteDataKeys.SiteMetaDescription),
            rows.GetValueOrDefault(SiteDataKeys.SiteOgImageUrl),
            BuiltinWorkerDisabled: rows.TryGetValue(SiteDataKeys.WorkerBuiltinDisabled, out var flag) &&
                                   string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase),
            IncidentCorrelationMode: rows.TryGetValue(SiteDataKeys.IncidentCorrelationMode, out var mode) &&
                Enum.TryParse<IncidentCorrelationMode>(mode, out var modeVal) ? modeVal : IncidentCorrelationMode.Hybrid,
            GlobalIncidentThreshold: rows.TryGetValue(SiteDataKeys.IncidentGlobalThreshold, out var threshold) &&
                int.TryParse(threshold, out var thresholdVal) ? thresholdVal : 3,
            GlobalIncidentCorrelationWindowMinutes: rows.TryGetValue(SiteDataKeys.IncidentGlobalCorrelationWindowMinutes, out var window) &&
                int.TryParse(window, out var windowVal) ? windowVal : 5
        );
    }

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        await SetManyAsync(new Dictionary<string, string?> { [key] = value }, ct);
    }

    public async Task SetManyAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var (key, value) in values)
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
                    Key = key,
                    Value = value,
                    DataType = "string",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                row.Value = value;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
