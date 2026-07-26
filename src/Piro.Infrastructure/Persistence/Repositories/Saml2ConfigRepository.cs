using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class Saml2ConfigRepository(PiroDbContext db) : ISaml2ConfigRepository
{
    public Task<List<Saml2ProviderConfig>> GetAllAsync(CancellationToken ct = default) =>
        db.Saml2ProviderConfigs.OrderBy(p => p.DisplayName).ToListAsync(ct);

    public Task<List<Saml2ProviderConfig>> GetEnabledAsync(CancellationToken ct = default) =>
        db.Saml2ProviderConfigs.Where(p => p.IsEnabled).OrderBy(p => p.DisplayName).ToListAsync(ct);

    public Task<Saml2ProviderConfig?> GetByIdAsync(string id, CancellationToken ct = default) =>
        db.Saml2ProviderConfigs.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task UpsertAsync(Saml2ProviderConfig config, CancellationToken ct = default)
    {
        var existing = await db.Saml2ProviderConfigs.FindAsync([config.Id], ct);
        if (existing is null)
            db.Saml2ProviderConfigs.Add(config);
        else
            db.Entry(existing).CurrentValues.SetValues(config);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var existing = await db.Saml2ProviderConfigs.FindAsync([id], ct);
        if (existing is null)
            return;

        db.Saml2ProviderConfigs.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
