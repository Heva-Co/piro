using System.ComponentModel.DataAnnotations;

namespace Piro.Application.DTOs;

/// <summary>Safe provider info exposed to the sign-in page (no secrets).</summary>
public record Saml2ProviderInfo(string Id, string DisplayName);

/// <summary>Full provider config for the admin UI.</summary>
public record Saml2ProviderConfigDto(
    string Id,
    string DisplayName,
    string IdpEntityId,
    string IdpSsoUrl,
    bool HasSigningCertificate,
    string? SpEntityId,
    string? AllowedDomains,
    string DefaultRole,
    bool IsEnabled
);

public record UpsertSaml2ProviderRequest(
    string Id,
    string DisplayName,
    string IdpEntityId,
    [Url] string IdpSsoUrl,
    /// <summary>PEM/base64 IdP signing certificate. Null or empty means "keep existing".</summary>
    string? IdpSigningCertificate,
    /// <summary>Null or empty means auto-derive from site:url config.</summary>
    string? SpEntityId,
    string? AllowedDomains,
    string DefaultRole,
    bool IsEnabled
);
