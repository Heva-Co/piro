using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class OidcConfigRepository(PiroDbContext db) : IOidcConfigRepository
{
    private const string SsoOnlyKey = "sso:enforce_only";

    public async Task<bool> GetSsoOnlyAsync(CancellationToken ct = default)
    {
        var row = await db.SiteData.FirstOrDefaultAsync(s => s.Key == SsoOnlyKey, ct);
        return row is not null && row.Value == "true";
    }

    public async Task SetSsoOnlyAsync(bool value, CancellationToken ct = default)
    {
        var row = await db.SiteData.FirstOrDefaultAsync(s => s.Key == SsoOnlyKey, ct);
        if (row is null)
        {
            db.SiteData.Add(new SiteData
            {
                Key = SsoOnlyKey, Value = value ? "true" : "false",
                DataType = "boolean", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Value = value ? "true" : "false";
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }


    public Task<List<OidcProviderConfig>> GetAllAsync(CancellationToken ct = default) =>
        db.OidcProviderConfigs.OrderBy(p => p.DisplayName).ToListAsync(ct);

    public Task<List<OidcProviderConfig>> GetEnabledAsync(CancellationToken ct = default) =>
        db.OidcProviderConfigs.Where(p => p.IsEnabled).OrderBy(p => p.DisplayName).ToListAsync(ct);

    public Task<OidcProviderConfig?> GetByIdAsync(string id, CancellationToken ct = default) =>
        db.OidcProviderConfigs.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task UpsertAsync(OidcProviderConfig config, CancellationToken ct = default)
    {
        var existing = await db.OidcProviderConfigs.FindAsync([config.Id], ct);
        if (existing is null)
            db.OidcProviderConfigs.Add(config);
        else
            db.Entry(existing).CurrentValues.SetValues(config);

        await db.SaveChangesAsync(ct);
    }
}
