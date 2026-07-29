using Piro.Domain.Auditing;

namespace Piro.Domain.Entities;

/// <summary>Persisted configuration for a SAML 2.0 SSO identity provider.</summary>
public class Saml2ProviderConfig : IAuditable
{
    /// <summary>Provider identifier: "okta", "keycloak", "azure", or any custom slug.</summary>
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The IdP's SAML entity ID (issuer), e.g. https://idp.example.com/realms/piro.</summary>
    public string IdpEntityId { get; set; } = string.Empty;

    /// <summary>The IdP's SingleSignOnService URL (HTTP-Redirect binding) the AuthnRequest is sent to.</summary>
    public string IdpSsoUrl { get; set; } = string.Empty;

    /// <summary>
    /// The IdP's signing certificate (public) as base64/PEM, used to verify the SAMLResponse signature.
    /// A public certificate is not a secret, so it is stored as plain text like other provider config.
    /// </summary>
    public string IdpSigningCertificate { get; set; } = string.Empty;

    /// <summary>
    /// The service provider (Piro) entity ID advertised in the AuthnRequest. When null/empty,
    /// auto-derived from site:url config as "{baseUrl}/saml/metadata".
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>Comma-separated allowed email domains. Null or empty = any domain.</summary>
    public string? AllowedDomains { get; set; }

    /// <summary>Role assigned to new users on first SSO login. Cannot be "Owner".</summary>
    public string DefaultRole { get; set; } = "Member";

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
