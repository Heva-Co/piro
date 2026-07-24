using Microsoft.Extensions.Caching.Distributed;
using Piro.Application.Interfaces;
using Piro.Contracts;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// On-demand OAuth access-token provider: returns a valid token, refreshing (once, under a
/// distributed lock) when the stored one is near expiry. Backed by the persistent, encrypted
/// <see cref="IOAuthTokenStore"/>. See <see cref="IOAuthTokenProvider"/> for the concurrency rationale.
/// </summary>
internal class OAuthTokenProvider(
    IIntegrationRepository integrationRepo,
    IOAuthTokenStore tokenStore,
    IOAuthClient oauthClient,
    IDistributedCache cache) : IOAuthTokenProvider
{
    // Refresh a bit before the real expiry so an in-flight call doesn't race the deadline.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);

    public async Task<string> GetAccessTokenAsync(Guid integrationId, CancellationToken ct = default)
    {
        var tokens = await tokenStore.GetAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} is not OAuth-connected.");

        if (!IsNearExpiry(tokens.ExpiresAt))
            return tokens.AccessToken;

        if (tokens.RefreshToken is null)
            throw new InvalidOperationException(
                $"Integration {integrationId}'s access token expired and no refresh token is available — reconnect required.");

        return await RefreshUnderLockAsync(integrationId, tokens.RefreshToken, ct);
    }

    private async Task<string> RefreshUnderLockAsync(Guid integrationId, string refreshToken, CancellationToken ct)
    {
        var lockKey = $"oauth:integration:refresh-lock:{integrationId}";

        // Try to acquire the lock. IDistributedCache has no atomic set-if-absent, so we use a
        // short-lived marker: if another instance already holds it, we wait and re-read the token
        // it will have rotated, instead of refreshing again with the same (soon-invalid) refresh token.
        var existingLock = await cache.GetStringAsync(lockKey, ct);
        if (existingLock is not null)
            return await WaitForRefreshedTokenAsync(integrationId, ct);

        await cache.SetStringAsync(lockKey, "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockTtl }, ct);
        try
        {
            // Re-read inside the lock: another instance may have refreshed between our check and acquire.
            var current = await tokenStore.GetAsync(integrationId, ct);
            if (current is not null && !IsNearExpiry(current.ExpiresAt))
                return current.AccessToken;

            var providerId = ResolveProviderId(await GetIntegrationTypeAsync(integrationId, ct));
            var refreshed = await oauthClient.RefreshAsync(integrationId, providerId, refreshToken, ct);

            // A provider may or may not rotate the refresh token; keep the old one if none returned.
            var toStore = refreshed.RefreshToken is null
                ? refreshed with { RefreshToken = refreshToken }
                : refreshed;
            await tokenStore.SaveAsync(integrationId, toStore, ct);

            return refreshed.AccessToken;
        }
        finally
        {
            await cache.RemoveAsync(lockKey, ct);
        }
    }

    private async Task<string> WaitForRefreshedTokenAsync(Guid integrationId, CancellationToken ct)
    {
        // Poll briefly for the lock holder to persist the rotated token.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            var tokens = await tokenStore.GetAsync(integrationId, ct);
            if (tokens is not null && !IsNearExpiry(tokens.ExpiresAt))
                return tokens.AccessToken;
        }
        throw new InvalidOperationException(
            $"Timed out waiting for a concurrent token refresh for integration {integrationId}.");
    }

    private async Task<string> GetIntegrationTypeAsync(Guid integrationId, CancellationToken ct)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");
        return integration.Type;
    }

    private static bool IsNearExpiry(DateTime expiresAt) => expiresAt - DateTime.UtcNow <= ExpiryMargin;

    /// <summary>Maps an integration id to its OAuth provider descriptor id. Extend as providers are added.</summary>
    private static string ResolveProviderId(string integrationId) => integrationId switch
    {
        "Jira" => "jira",
        _ => throw new InvalidOperationException($"Integration '{integrationId}' has no OAuth provider.")
    };
}
