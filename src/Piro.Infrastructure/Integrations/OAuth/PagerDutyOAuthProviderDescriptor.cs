namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// PagerDuty's OAuth 2.0 provider details. PagerDuty uses Scoped OAuth (authorization code + PKCE)
/// with refresh tokens; the Events API v2 routing key that alerts are ultimately sent with is
/// <i>discovered</i> via the REST API using the token obtained here (RFC 0004 §4.4) — the OAuth
/// token itself never triggers events.
/// </summary>
public sealed class PagerDutyOAuthProviderDescriptor : IOAuthProviderDescriptor
{
    public string ProviderId => "pagerduty";

    // Scoped OAuth endpoints. Verify against the live PagerDuty app-registration UI before relying
    // on these in production (RFC 0004 §8 flags exact scope strings / endpoints as implementation-time checks).
    public string AuthorizationEndpoint => "https://identity.pagerduty.com/oauth/authorize";
    public string TokenEndpoint => "https://identity.pagerduty.com/oauth/token";

    // services.read lists services and reads each service's Events API v2 integration_key;
    // services.write lets Piro provision a fresh Events API v2 integration on a service that has none.
    public string DefaultScopes => "services.read services.write";
}
