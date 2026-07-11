using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Application.Services;

/// <summary>
/// Manages OIDC/OAuth2 provider configuration and the authorization code + PKCE sign-in flow.
/// </summary>
public class OidcService(
    IOidcConfigRepository configRepo,
    ISiteConfigRepository siteConfigRepo,
    IConfiguration configuration,
    IDistributedCache cache,
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    ITokenService tokenService,
    IHttpClientFactory httpClientFactory) : IOidcService
{

    // ── Public contract ──────────────────────────────────────────────────────

    public async Task<List<OidcProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default) =>
        (await configRepo.GetEnabledAsync(ct))
            .Select(p => new OidcProviderInfo(p.Id, p.DisplayName))
            .ToList();

    public async Task<List<OidcProviderConfigDto>> GetAllConfigsAsync(CancellationToken ct = default) =>
        (await configRepo.GetAllAsync(ct))
            .Select(p => p.ToDto())
            .ToList();

    public async Task UpsertConfigAsync(UpsertOidcProviderRequest request, CancellationToken ct = default)
    {
        if (request.Id.Equals("owner", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("'owner' is a reserved provider ID.");

        var existing = await configRepo.GetByIdAsync(request.Id, ct);

        string clientSecret;
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            clientSecret = request.ClientSecret;
        else if (existing is not null)
            clientSecret = existing.ClientSecret; // keep existing
        else
            throw new InvalidOperationException("Client secret is required when creating a new provider.");

        // If this update would disable the only enabled provider while SSO-only mode is active,
        // it would lock every user out (no password sign-in, no working SSO) — same guard as
        // SetSsoOnlyModeAsync, approached from the provider-edit side instead.
        if (!request.IsEnabled && existing is { IsEnabled: true } && await configRepo.GetSsoOnlyAsync(ct))
        {
            var enabled = await configRepo.GetEnabledAsync(ct);
            if (enabled.Count == 1 && enabled[0].Id == existing.Id)
                throw new InvalidOperationException(
                    "Cannot disable the only enabled SSO provider while SSO-only mode is active.");
        }

        var normalizedAllowedDomains = NormalizeAllowedDomains(request.AllowedDomains);

        var config = existing ?? new OidcProviderConfig();
        config.Id = request.Id.ToLowerInvariant();
        config.DisplayName = request.DisplayName;
        config.Authority = request.Authority.TrimEnd('/');
        config.ClientId = request.ClientId;
        config.ClientSecret = clientSecret;
        config.RedirectUri = string.IsNullOrWhiteSpace(request.RedirectUri) ? null : request.RedirectUri;
        config.Scopes = request.Scopes;
        config.AllowedDomains = normalizedAllowedDomains;
        config.DefaultRole = request.DefaultRole == "Owner" ? "Member" : request.DefaultRole;
        config.IsEnabled = request.IsEnabled;

        await configRepo.UpsertAsync(config, ct);
    }

    private async Task<string> ResolveRedirectUriAsync(OidcProviderConfig config, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(config.RedirectUri))
            return config.RedirectUri;

        // Prefer site URL stored in DB (Configuration → General → Site URL)
        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/');

        // Fall back to env var
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Site URL is not configured. Set it in Configuration → General or via the App__BaseUrl env var.");

        return $"{baseUrl}/admin/auth/oidc/callback";
    }

    public async Task<string> GetStartUrlAsync(string providerId, CancellationToken ct = default)
    {
        var config = await configRepo.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException($"OIDC provider '{providerId}' not found or not enabled.");

        if (!config.IsEnabled)
            throw new InvalidOperationException($"OIDC provider '{providerId}' is disabled.");

        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            throw new InvalidOperationException(
                $"OIDC provider '{providerId}' has no client secret configured. " +
                "Re-enter the secret in Configuration → SSO.");

        var discovery = await GetDiscoveryDocumentAsync(config.Authority, ct);

        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        // Cache PKCE verifier keyed by state (10-minute TTL)
        var cacheKey = $"oidc:state:{state}";
        var payload = JsonSerializer.Serialize(new OidcStatePayload(providerId, verifier));
        await cache.SetStringAsync(cacheKey, payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        }, ct);

        var redirectUri = await ResolveRedirectUriAsync(config, ct);
        var scopes = NormalizeScopes(config.Scopes);
        return BuildAuthorizationUrl(discovery.AuthorizationEndpoint, config.ClientId, redirectUri, scopes, state, challenge);
    }

    public async Task<SignInResponse> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        // Retrieve and validate state
        var cacheKey = $"oidc:state:{state}";
        var payloadJson = await cache.GetStringAsync(cacheKey, ct)
            ?? throw new InvalidOperationException("Invalid or expired OIDC state. Please try signing in again.");
        await cache.RemoveAsync(cacheKey, ct);

        var statePayload = JsonSerializer.Deserialize<OidcStatePayload>(payloadJson)!;
        var config = await configRepo.GetByIdAsync(statePayload.ProviderId, ct)
            ?? throw new InvalidOperationException("OIDC provider configuration not found.");

        var discovery = await GetDiscoveryDocumentAsync(config.Authority, ct);
        var redirectUri = await ResolveRedirectUriAsync(config, ct);

        // Exchange authorization code for tokens
        var http = httpClientFactory.CreateClient("oidc-http");
        var userInfo = await ExchangeAndFetchUserInfoAsync(http, discovery, config, code, statePayload.CodeVerifier, config.ClientSecret, redirectUri, ct);

        // Domain restriction
        if (!string.IsNullOrWhiteSpace(config.AllowedDomains))
        {
            var emailParts = userInfo.Email.Split('@');
            if (emailParts.Length != 2 || string.IsNullOrWhiteSpace(emailParts[0]) || string.IsNullOrWhiteSpace(emailParts[1]))
                throw new InvalidOperationException($"OIDC userinfo returned a malformed email address: '{userInfo.Email}'.");

            var allowed = config.AllowedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var domain = emailParts[1];
            if (!allowed.Any(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Email domain '@{domain}' is not allowed for this SSO provider.");
        }

        var user = await UpsertUserAsync(userInfo, config.Id, config.DefaultRole, ct);
        return await BuildResponseAsync(user);
    }

    public async Task<bool> TestProviderAsync(string providerId, CancellationToken ct = default)
    {
        var config = await configRepo.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException($"Provider '{providerId}' not found.");
        return await TestAuthorityAsync(config.Authority, ct);
    }

    public async Task<bool> TestAuthorityAsync(string authority, CancellationToken ct = default)
    {
        var discovery = await GetDiscoveryDocumentAsync(authority, ct);

        if (string.IsNullOrWhiteSpace(discovery.AuthorizationEndpoint)
            || string.IsNullOrWhiteSpace(discovery.TokenEndpoint)
            || string.IsNullOrWhiteSpace(discovery.UserInfoEndpoint))
        {
            throw new InvalidOperationException(
                "Discovery document is missing authorization_endpoint, token_endpoint, or userinfo_endpoint.");
        }

        return true;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static readonly Regex DomainPattern = new(
        @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Validates and normalizes a comma-separated AllowedDomains list into a canonical
    /// lowercase, trimmed form. Rejects entries containing '@', whitespace, or anything
    /// else that isn't a bare domain — the previous behavior silently accepted garbage
    /// (e.g. "example.com " or "@example.com") that only surfaced as a confusing
    /// "domain not allowed" error at sign-in time.
    /// </summary>
    private static string? NormalizeAllowedDomains(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var domains = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.ToLowerInvariant())
            .ToList();

        var invalid = domains.Where(d => !DomainPattern.IsMatch(d)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Invalid domain(s) in Allowed Email Domains: {string.Join(", ", invalid)}. Use bare domains like 'example.com', comma-separated.");

        return domains.Count > 0 ? string.Join(",", domains) : null;
    }

    private async Task<OidcDiscoveryDocument> GetDiscoveryDocumentAsync(string authority, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("oidc-http");
        var url = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        var doc = await http.GetFromJsonAsync<OidcDiscoveryDocument>(url, ct)
            ?? throw new InvalidOperationException($"Failed to fetch OIDC discovery document from {url}.");
        return doc;
    }

    private static string BuildAuthorizationUrl(
        string authEndpoint, string clientId, string redirectUri,
        string scopes, string state, string codeChallenge) =>
        $"{authEndpoint}?" +
        $"response_type=code" +
        $"&client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&scope={Uri.EscapeDataString(scopes)}" +
        $"&state={state}" +
        $"&code_challenge={codeChallenge}" +
        $"&code_challenge_method=S256";

    private static string NormalizeScopes(string configured)
    {
        var parts = configured
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Union(["openid", "email", "profile"])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(" ", parts);
    }

    private async Task<OidcUserInfo> ExchangeAndFetchUserInfoAsync(
        HttpClient http,
        OidcDiscoveryDocument discovery,
        OidcProviderConfig config,
        string code,
        string codeVerifier,
        string clientSecret,
        string redirectUri,
        CancellationToken ct)
    {
        // Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = codeVerifier,
        });

        var tokenResponse = await http.PostAsync(discovery.TokenEndpoint, tokenRequest, ct);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        // Fetch user info from userinfo endpoint
        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, discovery.UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await http.SendAsync(userInfoRequest, ct);
        userInfoResponse.EnsureSuccessStatusCode();

        var userInfoJson = await userInfoResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var sub = GetStringClaim(userInfoJson, "sub")
            ?? throw new InvalidOperationException("OIDC userinfo response missing 'sub' claim.");
        var email = GetStringClaim(userInfoJson, "email")
            ?? throw new InvalidOperationException("OIDC userinfo response missing 'email' claim.");
        var name = GetStringClaim(userInfoJson, "name")
            ?? GetStringClaim(userInfoJson, "given_name")
            ?? email.Split('@')[0];

        return new OidcUserInfo(sub, email, name);
    }

    private async Task<AppUser> UpsertUserAsync(OidcUserInfo info, string providerId, string defaultRole, CancellationToken ct)
    {
        // Look up by ExternalId + ExternalProvider (existing SSO user)
        var existing = userManager.Users
            .FirstOrDefault(u => u.ExternalId == info.Sub && u.ExternalProvider == providerId);

        if (existing is not null)
        {
            // Keep name/email in sync
            if (existing.Name != info.Name || existing.Email != info.Email)
            {
                existing.Name = info.Name;
                existing.Email = info.Email;
                existing.UserName = info.Email;
                await userManager.UpdateAsync(existing);
            }
            return existing;
        }

        // First-time SSO login — check if local account with same email exists
        var byEmail = await userManager.FindByEmailAsync(info.Email);
        if (byEmail is not null)
        {
            // Link external identity to existing local account
            byEmail.ExternalId = info.Sub;
            byEmail.ExternalProvider = providerId;
            byEmail.IsActive = true;
            if (string.IsNullOrEmpty(byEmail.Name)) byEmail.Name = info.Name;
            await userManager.UpdateAsync(byEmail);
            return byEmail;
        }

        // Brand-new user — auto-provision
        var newUser = new AppUser
        {
            UserName = info.Email,
            Email = info.Email,
            Name = info.Name,
            ExternalId = info.Sub,
            ExternalProvider = providerId,
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        var role = await roleManager.FindByNameAsync(defaultRole) is not null ? defaultRole : "Member";
        await userManager.AddToRoleAsync(newUser, role);

        return newUser;
    }

    private async Task<SignInResponse> BuildResponseAsync(AppUser user)
    {
        var (accessToken, expires) = await tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var expiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds;

        return new SignInResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new UserDto(user.Id, user.Email!, user.Name, roles));
    }

    private static string? GetStringClaim(JsonElement json, string key) =>
        json.TryGetProperty(key, out var prop) ? prop.GetString() : null;

    public Task<bool> GetSsoOnlyModeAsync(CancellationToken ct = default) =>
        configRepo.GetSsoOnlyAsync(ct);

    public async Task SetSsoOnlyModeAsync(bool value, CancellationToken ct = default)
    {
        if (value)
        {
            var enabled = await configRepo.GetEnabledAsync(ct);
            if (enabled.Count == 0)
                throw new InvalidOperationException(
                    "Cannot enable SSO-only mode: at least one enabled SSO provider is required.");
        }

        await configRepo.SetSsoOnlyAsync(value, ct);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── Inner types ──────────────────────────────────────────────────────────

    private record OidcStatePayload(string ProviderId, string CodeVerifier);
    private record OidcUserInfo(string Sub, string Email, string Name);

    private sealed class OidcDiscoveryDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("userinfo_endpoint")]
        public string UserInfoEndpoint { get; set; } = string.Empty;
    }
}
