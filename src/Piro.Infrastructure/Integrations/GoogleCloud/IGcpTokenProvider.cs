namespace Piro.Infrastructure.Integrations.GoogleCloud;

/// <summary>
/// Obtains and caches OAuth2 access tokens for Google Cloud service accounts — see <see cref="GcpTokenProvider"/>.
/// </summary>
public interface IGcpTokenProvider
{
    /// <summary>
    /// Returns a valid access token for the given integration, fetching a new one if the cached
    /// token is missing or within 5 minutes of expiry.
    /// </summary>
    /// <param name="integrationId">Used as the cache key.</param>
    /// <param name="configJson">The integration's ConfigJson — must contain a "serviceAccountJson" string field.</param>
    Task<string> GetAccessTokenAsync(Guid integrationId, string configJson, CancellationToken ct = default);
}
