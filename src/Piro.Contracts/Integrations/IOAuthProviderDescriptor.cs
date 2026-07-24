namespace Piro.Contracts;

/// <summary>
/// The fixed, per-provider protocol details of a third-party OAuth service. This is the
/// <b>provider-specific</b> slice of the generic OAuth framework: the generic flow
/// (<see cref="IOAuthClient"/>) knows how to do authorization-code + PKCE and refresh; a
/// descriptor supplies only what differs between GitHub, Jira, etc. — their
/// endpoints and default scopes. Client credentials (id/secret) are per-installation data and
/// live in each integration's ConfigJson, not here.
/// </summary>
public interface IOAuthProviderDescriptor
{
    /// <summary>Provider key for the integration type ("jira").</summary>
    string ProviderId { get; }

    /// <summary>Authorization endpoint the user's browser is redirected to.</summary>
    string AuthorizationEndpoint { get; }

    /// <summary>Token endpoint for the code-for-token exchange and refresh.</summary>
    string TokenEndpoint { get; }

    /// <summary>Default space-separated scopes when the config doesn't override them.</summary>
    string DefaultScopes { get; }
}
