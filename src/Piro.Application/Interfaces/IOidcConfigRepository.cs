using Piro.Domain.Entities;

namespace Piro.Application.Interfaces;

public interface IOidcConfigRepository
{
    Task<List<OidcProviderConfig>> GetAllAsync(CancellationToken ct = default);
    Task<List<OidcProviderConfig>> GetEnabledAsync(CancellationToken ct = default);
    Task<OidcProviderConfig?> GetByIdAsync(string id, CancellationToken ct = default);
    Task UpsertAsync(OidcProviderConfig config, CancellationToken ct = default);

    Task<bool> GetSsoOnlyAsync(CancellationToken ct = default);
    Task SetSsoOnlyAsync(bool value, CancellationToken ct = default);
}
