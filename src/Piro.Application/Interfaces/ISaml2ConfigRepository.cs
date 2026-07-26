using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface ISaml2ConfigRepository
{
    Task<List<Saml2ProviderConfig>> GetAllAsync(CancellationToken ct = default);
    Task<List<Saml2ProviderConfig>> GetEnabledAsync(CancellationToken ct = default);
    Task<Saml2ProviderConfig?> GetByIdAsync(string id, CancellationToken ct = default);
    Task UpsertAsync(Saml2ProviderConfig config, CancellationToken ct = default);
}
