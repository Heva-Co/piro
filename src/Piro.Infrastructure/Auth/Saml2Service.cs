using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Auth;

/// <summary>
/// Manages SAML 2.0 provider configuration and the SP-initiated redirect/POST sign-in flow.
/// Uses ITfoxtec.Identity.Saml2 for AuthnRequest generation and SAMLResponse signature/condition
/// validation; user provisioning and token issuance are delegated to the shared
/// <see cref="ISsoUserProvisioner"/> so the SAML and OIDC flows converge on identical logic.
/// </summary>
internal class Saml2Service(
    ISaml2ConfigRepository configRepo,
    ISiteConfigRepository siteConfigRepo,
    IConfiguration configuration,
    IDistributedCache cache,
    ISsoUserProvisioner provisioner) : ISaml2Service
{
    // ── Public contract ──────────────────────────────────────────────────────

    public async Task<List<Saml2ProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default) =>
        (await configRepo.GetEnabledAsync(ct))
            .Select(p => new Saml2ProviderInfo(p.Id, p.DisplayName))
            .ToList();

    public async Task<List<Saml2ProviderConfigDto>> GetAllConfigsAsync(CancellationToken ct = default) =>
        (await configRepo.GetAllAsync(ct))
            .Select(p => p.ToDto())
            .ToList();

    public async Task UpsertConfigAsync(UpsertSaml2ProviderRequest request, CancellationToken ct = default)
    {
        if (request.Id.Equals("owner", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("'owner' is a reserved provider ID.");

        var existing = await configRepo.GetByIdAsync(request.Id, ct);

        string signingCertificate;
        if (!string.IsNullOrWhiteSpace(request.IdpSigningCertificate))
        {
            // Validate it parses before persisting — a bad cert should fail on save, not at sign-in.
            _ = ParseCertificate(request.IdpSigningCertificate);
            signingCertificate = request.IdpSigningCertificate.Trim();
        }
        else if (existing is not null)
        {
            signingCertificate = existing.IdpSigningCertificate; // keep existing
        }
        else
        {
            throw new InvalidOperationException("An IdP signing certificate is required when creating a new provider.");
        }

        var config = existing ?? new Saml2ProviderConfig();
        config.Id = request.Id.ToLowerInvariant();
        config.DisplayName = request.DisplayName;
        config.IdpEntityId = request.IdpEntityId;
        config.IdpSsoUrl = request.IdpSsoUrl;
        config.IdpSigningCertificate = signingCertificate;
        config.SpEntityId = string.IsNullOrWhiteSpace(request.SpEntityId) ? null : request.SpEntityId;
        config.AllowedDomains = string.IsNullOrWhiteSpace(request.AllowedDomains) ? null : request.AllowedDomains.Trim();
        config.DefaultRole = request.DefaultRole == "Owner" ? "Member" : request.DefaultRole;
        config.IsEnabled = request.IsEnabled;

        await configRepo.UpsertAsync(config, ct);
    }

    public async Task<string> GetStartUrlAsync(string providerId, CancellationToken ct = default)
    {
        var config = await configRepo.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException($"SAML provider '{providerId}' not found.");

        if (!config.IsEnabled)
            throw new InvalidOperationException($"SAML provider '{providerId}' is disabled.");

        var saml2Config = await BuildConfigurationAsync(config, ct);

        // RelayState carries the provider id back to the ACS endpoint (which provider validated
        // this response), cached briefly to also defend against unsolicited responses.
        var relayState = Guid.NewGuid().ToString("N");
        await cache.SetStringAsync($"saml:relay:{relayState}", providerId, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        }, ct);

        var binding = new Saml2RedirectBinding();
        binding.SetRelayStateQuery(new Dictionary<string, string> { { "rs", relayState } });

        var authnRequest = new Saml2AuthnRequest(saml2Config)
        {
            AssertionConsumerServiceUrl = new Uri(await ResolveAcsUrlAsync(ct)),
        };

        binding.Bind(authnRequest);
        return binding.RedirectLocation.OriginalString;
    }

    public async Task<Saml2AcsResult> HandleAcsAsync(string samlResponse, string? relayState, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(samlResponse))
            throw new InvalidOperationException("Missing SAMLResponse.");

        // Resolve which provider this response belongs to via RelayState.
        var rs = relayState;
        var parsedRelay = TryReadRelayState(relayState);
        if (parsedRelay is not null) rs = parsedRelay;

        if (string.IsNullOrWhiteSpace(rs))
            throw new InvalidOperationException("Missing or invalid RelayState.");

        var providerId = await cache.GetStringAsync($"saml:relay:{rs}", ct)
            ?? throw new InvalidOperationException("Invalid or expired SAML RelayState. Please try signing in again.");
        await cache.RemoveAsync($"saml:relay:{rs}", ct);

        var config = await configRepo.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException("SAML provider configuration not found.");

        var saml2Config = await BuildConfigurationAsync(config, ct);

        // Feed ITfoxtec its own HttpRequest abstraction built from the raw form values, so the
        // Application-layer contract stays free of ASP.NET types.
        var itfoxtecRequest = new ITfoxtec.Identity.Saml2.Http.HttpRequest
        {
            Method = "POST",
            Form = new NameValueCollection { { "SAMLResponse", samlResponse } },
        };

        var authnResponse = new Saml2AuthnResponse(saml2Config);
        itfoxtecRequest.Binding = new Saml2PostBinding();
        itfoxtecRequest.Binding.ReadSamlResponse(itfoxtecRequest, authnResponse);

        if (authnResponse.Status != Saml2StatusCodes.Success)
            throw new InvalidOperationException($"SAML sign-in failed with status '{authnResponse.Status}'.");

        // Unbind validates the XML signature against the configured signing certificate.
        itfoxtecRequest.Binding.Unbind(itfoxtecRequest, authnResponse);

        var (email, name) = ExtractIdentity(authnResponse);
        var subject = authnResponse.NameId?.Value
            ?? throw new InvalidOperationException("SAML assertion is missing a NameID.");

        var externalUser = new ExternalUserInfo(subject, email, name);
        var signIn = await provisioner.ProvisionAndSignInAsync(externalUser, config.Id, config.DefaultRole, config.AllowedDomains, ct);
        return new Saml2AcsResult(signIn, rs);
    }

    public async Task<bool> TestProviderAsync(string providerId, CancellationToken ct = default)
    {
        var config = await configRepo.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException($"Provider '{providerId}' not found.");

        // Config is internally consistent if the cert parses and the required endpoints are present.
        _ = ParseCertificate(config.IdpSigningCertificate);

        if (string.IsNullOrWhiteSpace(config.IdpEntityId) || string.IsNullOrWhiteSpace(config.IdpSsoUrl))
            throw new InvalidOperationException("Provider is missing an IdP entity ID or SSO URL.");

        return true;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Saml2Configuration> BuildConfigurationAsync(Saml2ProviderConfig config, CancellationToken ct)
    {
        var spEntityId = await ResolveSpEntityIdAsync(config, ct);
        var saml2Config = new Saml2Configuration
        {
            Issuer = spEntityId,
            SingleSignOnDestination = new Uri(config.IdpSsoUrl),
            AllowedIssuer = config.IdpEntityId,
        };
        saml2Config.SignatureValidationCertificates.Add(ParseCertificate(config.IdpSigningCertificate));
        return saml2Config;
    }

    private static X509Certificate2 ParseCertificate(string pemOrBase64)
    {
        var raw = pemOrBase64.Trim();
        try
        {
            if (raw.Contains("BEGIN CERTIFICATE"))
                return X509Certificate2.CreateFromPem(raw);

            return new X509Certificate2(Convert.FromBase64String(raw));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The IdP signing certificate could not be parsed: {ex.Message}");
        }
    }

    private static (string Email, string Name) ExtractIdentity(Saml2AuthnResponse response)
    {
        var identity = response.ClaimsIdentity;

        var email = FindClaim(identity,
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                "urn:oid:1.2.840.113549.1.9.1.1",
                "email")
            ?? response.NameId?.Value
            ?? throw new InvalidOperationException("SAML assertion did not include an email address.");

        var name = FindClaim(identity,
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
                "urn:oid:2.16.840.1.113730.3.1.241",
                "displayName",
                "name")
            ?? email.Split('@')[0];

        return (email, name);
    }

    private static string? FindClaim(System.Security.Claims.ClaimsIdentity? identity, params string[] types)
    {
        if (identity is null) return null;
        foreach (var type in types)
        {
            var value = identity.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    /// <summary>Extracts our "rs" token from a RelayState that may itself be a query string (rs=...).</summary>
    private static string? TryReadRelayState(string? relayState)
    {
        if (string.IsNullOrWhiteSpace(relayState)) return null;
        if (!relayState.Contains('=')) return null;

        var query = System.Web.HttpUtility.ParseQueryString(relayState);
        return query["rs"];
    }

    private async Task<string> ResolveAcsUrlAsync(CancellationToken ct) =>
        $"{await ResolveBaseUrlAsync(ct)}/api/v1/auth/saml/acs";

    private async Task<string> ResolveSpEntityIdAsync(Saml2ProviderConfig config, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(config.SpEntityId))
            return config.SpEntityId;

        return $"{await ResolveBaseUrlAsync(ct)}/saml/metadata";
    }

    private async Task<string> ResolveBaseUrlAsync(CancellationToken ct)
    {
        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Site URL is not configured. Set it in Configuration → General or via the App__BaseUrl env var.");

        return baseUrl;
    }
}
