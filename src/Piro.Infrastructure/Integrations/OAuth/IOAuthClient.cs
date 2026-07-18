namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// The generic, provider-agnostic OAuth 2.0 client — the reusable core of the framework. It knows
/// how to run authorization-code + PKCE and refresh a token; it knows nothing about what any
/// provider's tokens are then used <i>for</i> (that is discovery/dispatch, provider-specific).
/// Generalizes the flow already implemented once for user sign-in in <c>OidcService</c>.
/// </summary>
public interface IOAuthClient
{
    /// <summary>
    /// Builds the authorization-URL the admin's browser is redirected to, and caches the PKCE
    /// verifier keyed by the returned <c>state</c> so <see cref="ExchangeCodeAsync"/> can complete
    /// the flow. Ties the pending connection to <paramref name="integrationId"/>.
    /// </summary>
    Task<string> BuildAuthorizationUrlAsync(string providerId, Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// Completes the flow: validates <paramref name="state"/> against the cached PKCE entry,
    /// exchanges <paramref name="code"/> for tokens, and returns them plus the integration id the
    /// connection was started for. Does not persist — the caller stores the tokens.
    /// </summary>
    Task<OAuthCallbackResult> ExchangeCodeAsync(string code, string state, CancellationToken ct = default);

    /// <summary>Exchanges a refresh token for a fresh token set (<c>grant_type=refresh_token</c>), using the integration's stored credentials.</summary>
    Task<OAuthTokenSet> RefreshAsync(Guid integrationId, string providerId, string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// The exact redirect URL this client sends to the provider — the same value the admin must
    /// register in the provider's OAuth app. Exposed so the UI can display it instead of guessing.
    /// </summary>
    Task<string> GetRedirectUriAsync(CancellationToken ct = default);
}

/// <summary>Result of a completed authorization-code exchange.</summary>
public sealed record OAuthCallbackResult(Guid IntegrationId, string ProviderId, OAuthTokenSet Tokens);
