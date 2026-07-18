namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Hands out a valid access token for an OAuth-connected integration, refreshing transparently when
/// the stored token is near expiry. This is the generalization of <c>GcpTokenProvider</c> — but
/// backed by the persistent, encrypted <see cref="IOAuthTokenStore"/> instead of an in-memory cache,
/// and using authorization-code refresh tokens instead of a JWT-bearer grant.
/// <para>
/// Refresh is serialized with a distributed lock (RFC 0004 §4.3) so concurrent dispatches don't both
/// refresh with the same rotating refresh token (which the provider would invalidate on first use).
/// A proactive background refresh job is a later phase; this provider covers the on-demand path.
/// </para>
/// </summary>
public interface IOAuthTokenProvider
{
    /// <summary>
    /// Returns a currently-valid access token for the integration, refreshing if necessary.
    /// Throws if the integration is not OAuth-connected (no stored token).
    /// </summary>
    Task<string> GetAccessTokenAsync(Guid integrationId, CancellationToken ct = default);
}
