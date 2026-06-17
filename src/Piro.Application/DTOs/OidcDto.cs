namespace Piro.Application.DTOs;

/// <summary>Safe provider info exposed to the sign-in page (no secrets).</summary>
public record OidcProviderInfo(string Id, string DisplayName);

/// <summary>Full provider config for the admin UI (no client secret).</summary>
public record OidcProviderConfigDto(
    string Id,
    string DisplayName,
    string Authority,
    string ClientId,
    bool HasClientSecret,
    string? RedirectUri,
    string Scopes,
    string? AllowedDomains,
    string DefaultRole,
    bool IsEnabled
);

public record UpsertOidcProviderRequest(
    string Id,
    string DisplayName,
    string Authority,
    string ClientId,
    /// <summary>Null or empty means "keep existing secret".</summary>
    string? ClientSecret,
    /// <summary>Null or empty means auto-derive from site:url config.</summary>
    string? RedirectUri,
    string Scopes,
    string? AllowedDomains,
    string DefaultRole,
    bool IsEnabled
);
