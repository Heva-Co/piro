using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

/// <summary>Persistence for <see cref="ServiceIntegrationMapping"/> (RFC 0004 §4.5).</summary>
public interface IServiceIntegrationMappingRepository
{
    Task<List<ServiceIntegrationMapping>> GetByServiceIdAsync(int serviceId, CancellationToken ct = default);
    Task<List<ServiceIntegrationMapping>> GetByIntegrationIdAsync(Guid integrationId, CancellationToken ct = default);
    Task<ServiceIntegrationMapping?> GetAsync(int serviceId, Guid integrationId, CancellationToken ct = default);
    Task UpsertAsync(ServiceIntegrationMapping mapping, CancellationToken ct = default);
    Task DeleteAsync(int serviceId, Guid integrationId, CancellationToken ct = default);
}
