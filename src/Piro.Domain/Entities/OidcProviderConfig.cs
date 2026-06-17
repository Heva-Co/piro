namespace Piro.Domain.Entities;

/// <summary>Persisted configuration for an OIDC/OAuth2 SSO provider.</summary>
public class OidcProviderConfig
{
    /// <summary>Provider identifier: "google", "microsoft", "github", or any custom slug.</summary>
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>OIDC authority / issuer URL (e.g. https://accounts.google.com).</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Stored as plain text. DB-level access controls are the operator's responsibility.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Callback URL registered with the provider. When null/empty, auto-derived from site:url config.</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Space-separated scopes (e.g. "openid email profile"). openid is always added automatically.</summary>
    public string Scopes { get; set; } = "openid email profile";

    /// <summary>Comma-separated allowed email domains. Null or empty = any domain.</summary>
    public string? AllowedDomains { get; set; }

    /// <summary>Role assigned to new users on first SSO login. Cannot be "Owner".</summary>
    public string DefaultRole { get; set; } = "Member";

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
