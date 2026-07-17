using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Persistence.Repositories;

public class IntegrationRepository(PiroDbContext db) : IIntegrationRepository
{
    public async Task<IEnumerable<Integration>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Integrations
            .Include(i => i.Checks)
            .OrderBy(i => i.Name)
            .ToListAsync(ct);
    }

    public async Task<Integration?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Integrations
            .Include(i => i.Checks)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Integration> CreateAsync(Integration integration, CancellationToken ct = default)
    {
        db.Integrations.Add(integration);
        await db.SaveChangesAsync(ct);
        return integration;
    }

    public async Task<Integration> UpdateAsync(Integration integration, CancellationToken ct = default)
    {
        db.Integrations.Update(integration);
        await db.SaveChangesAsync(ct);
        return integration;
    }

    public async Task DeleteAsync(Integration integration, CancellationToken ct = default)
    {
        db.Integrations.Remove(integration);
        await db.SaveChangesAsync(ct);
    }
}
