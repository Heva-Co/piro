namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Atlassian (Jira Cloud) OAuth 2.0 (3LO) provider details (RFC 0012). Authorization code + PKCE with
/// refresh tokens; <c>offline_access</c> is required to be issued a refresh token. After connecting,
/// the Jira Cloud <c>cloudId</c> (and human-facing site URL) is discovered via
/// <c>GET https://api.atlassian.com/oauth/token/accessible-resources</c> and stored on the integration,
/// because 3LO API calls route through <c>https://api.atlassian.com/ex/jira/{cloudId}</c>, not the
/// site's own base URL.
/// </summary>
public sealed class JiraOAuthProviderDescriptor : IOAuthProviderDescriptor
{
    public string ProviderId => "jira";

    // Atlassian's 3LO endpoints (auth.atlassian.com), not a per-site URL — verify against the live
    // Atlassian developer console before relying on these in production.
    public string AuthorizationEndpoint => "https://auth.atlassian.com/authorize";
    public string TokenEndpoint => "https://auth.atlassian.com/oauth/token";

    // write:jira-work + read:jira-work to create/read issues; offline_access to receive a refresh token.
    public string DefaultScopes => "write:jira-work read:jira-work offline_access";
}
