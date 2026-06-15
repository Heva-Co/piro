using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

public interface IOidcService
{
    /// <summary>Returns enabled providers for display on the sign-in page.</summary>
    Task<List<OidcProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default);

    /// <summary>Returns all provider configs for the admin UI (no secrets).</summary>
    Task<List<OidcProviderConfigDto>> GetAllConfigsAsync(CancellationToken ct = default);

    /// <summary>Creates or updates a provider config.</summary>
    Task UpsertConfigAsync(UpsertOidcProviderRequest request, CancellationToken ct = default);

    /// <summary>Builds the authorization URL to redirect the user to the provider.</summary>
    Task<string> GetStartUrlAsync(string providerId, CancellationToken ct = default);

    /// <summary>Handles the callback: exchanges code, upserts user, returns Piro JWT pair.</summary>
    Task<SignInResponse> HandleCallbackAsync(string code, string state, CancellationToken ct = default);

    /// <summary>Verifies the provider is reachable by fetching its discovery document.</summary>
    Task<bool> TestProviderAsync(string providerId, CancellationToken ct = default);

    /// <summary>Returns whether SSO-only mode is active (password sign-in disabled).</summary>
    Task<bool> GetSsoOnlyModeAsync(CancellationToken ct = default);

    /// <summary>Enables or disables SSO-only mode.</summary>
    Task SetSsoOnlyModeAsync(bool value, CancellationToken ct = default);
}
