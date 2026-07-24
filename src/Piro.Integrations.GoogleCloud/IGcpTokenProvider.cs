namespace Piro.Integrations.GoogleCloud;

/// <summary>
/// Obtains and caches OAuth2 access tokens for Google Cloud service accounts — see <see cref="GcpTokenProvider"/>.
/// </summary>
public interface IGcpTokenProvider
{
    /// <summary>
    /// Returns a valid access token for the GoogleCloud integration instance, fetching a new one if the
    /// cached token is missing or near expiry. The provider resolves and decrypts that integration's
    /// service-account config itself, so a caller (e.g. the Cloud Run Job check) needs only the instance
    /// id — it never handles the raw credentials.
    /// </summary>
    Task<string> GetAccessTokenAsync(Guid integrationInstanceId, CancellationToken ct = default);
}
