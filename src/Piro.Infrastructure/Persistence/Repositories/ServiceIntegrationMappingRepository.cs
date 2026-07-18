using Microsoft.EntityFrameworkCore;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

internal class ServiceIntegrationMappingRepository(PiroDbContext db) : IServiceIntegrationMappingRepository
{
    public Task<List<ServiceIntegrationMapping>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default) =>
        db.ServiceIntegrationMappings.Where(m => m.ServiceId == serviceId).ToListAsync(ct);

    public Task<List<ServiceIntegrationMapping>> GetByIntegrationIdAsync(Guid integrationId, CancellationToken ct = default) =>
        db.ServiceIntegrationMappings.Where(m => m.IntegrationId == integrationId).ToListAsync(ct);

    public Task<ServiceIntegrationMapping?> GetAsync(int serviceId, Guid integrationId, CancellationToken ct = default) =>
        db.ServiceIntegrationMappings.FirstOrDefaultAsync(m => m.ServiceId == serviceId && m.IntegrationId == integrationId, ct);

    public async Task UpsertAsync(ServiceIntegrationMapping mapping, CancellationToken ct = default)
    {
        var existing = await db.ServiceIntegrationMappings
            .FirstOrDefaultAsync(m => m.ServiceId == mapping.ServiceId && m.IntegrationId == mapping.IntegrationId, ct);
        if (existing is null)
        {
            db.ServiceIntegrationMappings.Add(mapping);
        }
        else
        {
            existing.MappingJson = mapping.MappingJson;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int serviceId, Guid integrationId, CancellationToken ct = default)
    {
        var existing = await db.ServiceIntegrationMappings
            .FirstOrDefaultAsync(m => m.ServiceId == serviceId && m.IntegrationId == integrationId, ct);
        if (existing is null)
            return;
        db.ServiceIntegrationMappings.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
