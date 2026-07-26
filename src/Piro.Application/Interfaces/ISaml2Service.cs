using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>Outcome of a completed SAML2 assertion-consumer step.</summary>
public record Saml2AcsResult(SignInResponse SignIn, string? RelayState);

public interface ISaml2Service
{
    /// <summary>Returns enabled SAML providers for display on the sign-in page.</summary>
    Task<List<Saml2ProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default);

    /// <summary>Returns all provider configs for the admin UI (certificate as a boolean).</summary>
    Task<List<Saml2ProviderConfigDto>> GetAllConfigsAsync(CancellationToken ct = default);

    /// <summary>Creates or updates a provider config.</summary>
    Task UpsertConfigAsync(UpsertSaml2ProviderRequest request, CancellationToken ct = default);

    /// <summary>Builds the IdP redirect URL (SAML AuthnRequest, HTTP-Redirect binding) for the given provider.</summary>
    Task<string> GetStartUrlAsync(string providerId, CancellationToken ct = default);

    /// <summary>
    /// Handles the Assertion Consumer Service POST: validates the SAMLResponse signature and
    /// conditions, upserts the user, and returns Piro's JWT pair plus the original RelayState.
    /// </summary>
    Task<Saml2AcsResult> HandleAcsAsync(string samlResponse, string? relayState, CancellationToken ct = default);

    /// <summary>Verifies a saved provider's configuration is internally consistent (parseable cert, present endpoints).</summary>
    Task<bool> TestProviderAsync(string providerId, CancellationToken ct = default);
}
