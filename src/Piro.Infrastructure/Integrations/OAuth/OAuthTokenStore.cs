using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Piro.Domain.Entities;
using Piro.Infrastructure.Persistence;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// EF-backed token store that encrypts access/refresh tokens with <see cref="IDataProtector"/>
/// before persisting them and decrypts them on read. This is the first real consumer of the
/// <c>AddDataProtection()</c> registration that was previously wired up but unused.
/// </summary>
internal class OAuthTokenStore(PiroDbContext db, IDataProtectionProvider dataProtectionProvider) : IOAuthTokenStore
{
    // A dedicated purpose string isolates these tokens' key ring from any other protector use.
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Piro.OAuthTokens.v1");

    public async Task<OAuthTokenSet?> GetAsync(Guid integrationId, CancellationToken ct = default)
    {
        var row = await db.OAuthTokens.FirstOrDefaultAsync(t => t.IntegrationId == integrationId, ct);
        if (row is null)
            return null;

        return new OAuthTokenSet(
            _protector.Unprotect(row.AccessToken),
            row.RefreshToken is null ? null : _protector.Unprotect(row.RefreshToken),
            row.ExpiresAt,
            row.Scopes);
    }

    public async Task SaveAsync(Guid integrationId, OAuthTokenSet tokens, CancellationToken ct = default)
    {
        var row = await db.OAuthTokens.FirstOrDefaultAsync(t => t.IntegrationId == integrationId, ct);
        if (row is null)
        {
            row = new OAuthToken { IntegrationId = integrationId };
            db.OAuthTokens.Add(row);
        }

        row.AccessToken = _protector.Protect(tokens.AccessToken);
        row.RefreshToken = tokens.RefreshToken is null ? null : _protector.Protect(tokens.RefreshToken);
        row.ExpiresAt = tokens.ExpiresAt;
        row.Scopes = tokens.Scopes;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid integrationId, CancellationToken ct = default)
    {
        var row = await db.OAuthTokens.FirstOrDefaultAsync(t => t.IntegrationId == integrationId, ct);
        if (row is null)
            return;

        db.OAuthTokens.Remove(row);
        await db.SaveChangesAsync(ct);
    }
}
