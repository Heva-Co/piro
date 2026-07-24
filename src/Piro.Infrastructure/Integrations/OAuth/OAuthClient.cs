using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Contracts;

namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>
/// Generic authorization-code + PKCE OAuth client, generalizing the flow in <c>OidcService</c>
/// (PKCE generation, state cached in <see cref="IDistributedCache"/>, code-for-token exchange)
/// into a provider-agnostic form driven by <see cref="IOAuthProviderDescriptor"/>s.
/// <para>
/// The OAuth app credentials (client id/secret) live in each <see cref="Integration"/>'s ConfigJson —
/// there is no separate global provider-config table. The client secret is stored encrypted at rest
/// (<see cref="ISecretProtector"/>) and decrypted here only to perform the token exchange.
/// </para>
/// </summary>
internal class OAuthClient(
    IEnumerable<IOAuthProviderDescriptor> descriptors,
    IIntegrationRepository integrationRepo,
    Piro.Integrations.Abstractions.IIntegrationRegistry integrationRegistry,
    ISecretProtector secretProtector,
    ISiteConfigRepository siteConfigRepo,
    IConfiguration configuration,
    IDistributedCache cache,
    IHttpClientFactory httpClientFactory) : IOAuthClient
{
    private readonly Dictionary<string, IOAuthProviderDescriptor> _descriptors =
        descriptors.ToDictionary(d => d.ProviderId, StringComparer.OrdinalIgnoreCase);

    private const string HttpClientName = "oauth-integration-http";

    public async Task<string> BuildAuthorizationUrlAsync(string providerId, Guid integrationId, CancellationToken ct = default)
    {
        var descriptor = ResolveDescriptor(providerId);
        var creds = await ResolveCredentialsAsync(integrationId, ct);

        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        // Cache the PKCE verifier + which integration/provider this connection is for (10-min TTL),
        // keyed by state — same pattern as OidcService's oidc:state:{state}.
        var payload = JsonSerializer.Serialize(new OAuthStatePayload(providerId, integrationId, verifier));
        await cache.SetStringAsync(StateKey(state), payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        }, ct);

        var redirectUri = await ResolveRedirectUriAsync(ct);

        return QueryHelpers.AddQueryString(descriptor.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            [OAuthProtocol.ResponseType] = OAuthProtocol.ResponseTypeCode,
            [OAuthProtocol.ClientId] = creds.ClientId,
            [OAuthProtocol.RedirectUri] = redirectUri,
            [OAuthProtocol.Scope] = descriptor.DefaultScopes,
            [OAuthProtocol.State] = state,
            [OAuthProtocol.CodeChallenge] = challenge,
            [OAuthProtocol.CodeChallengeMethod] = OAuthProtocol.ChallengeMethodS256,
        });
    }

    public async Task<OAuthCallbackResult> ExchangeCodeAsync(string code, string state, CancellationToken ct = default)
    {
        var payloadJson = await cache.GetStringAsync(StateKey(state), ct)
            ?? throw new InvalidOperationException("Invalid or expired OAuth state. Please start the connection again.");
        await cache.RemoveAsync(StateKey(state), ct);

        var statePayload = JsonSerializer.Deserialize<OAuthStatePayload>(payloadJson)!;
        var descriptor = ResolveDescriptor(statePayload.ProviderId);
        var creds = await ResolveCredentialsAsync(statePayload.IntegrationId, ct);
        var redirectUri = await ResolveRedirectUriAsync(ct);

        var tokens = await ExchangeAsync(descriptor, new Dictionary<string, string>
        {
            [OAuthProtocol.GrantType] = OAuthProtocol.GrantAuthorizationCode,
            [OAuthProtocol.Code] = code,
            [OAuthProtocol.RedirectUri] = redirectUri,
            [OAuthProtocol.ClientId] = creds.ClientId,
            [OAuthProtocol.ClientSecret] = creds.ClientSecret,
            [OAuthProtocol.CodeVerifier] = statePayload.CodeVerifier,
        }, ct);

        return new OAuthCallbackResult(statePayload.IntegrationId, statePayload.ProviderId, tokens);
    }

    public async Task<OAuthTokenSet> RefreshAsync(Guid integrationId, string providerId, string refreshToken, CancellationToken ct = default)
    {
        var descriptor = ResolveDescriptor(providerId);
        var creds = await ResolveCredentialsAsync(integrationId, ct);

        return await ExchangeAsync(descriptor, new Dictionary<string, string>
        {
            [OAuthProtocol.GrantType] = OAuthProtocol.GrantRefreshToken,
            [OAuthProtocol.RefreshToken] = refreshToken,
            [OAuthProtocol.ClientId] = creds.ClientId,
            [OAuthProtocol.ClientSecret] = creds.ClientSecret,
        }, ct);
    }

    private async Task<OAuthTokenSet> ExchangeAsync(
        IOAuthProviderDescriptor descriptor,
        Dictionary<string, string> form, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        var response = await http.PostAsync(descriptor.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var accessToken = json.GetProperty(OAuthProtocol.AccessTokenField).GetString()
            ?? throw new InvalidOperationException("Token response missing access_token.");
        var refreshToken = json.TryGetProperty(OAuthProtocol.RefreshTokenField, out var rt) ? rt.GetString() : null;
        var scopes = json.TryGetProperty(OAuthProtocol.ScopeField, out var sc) ? sc.GetString() : null;

        // Read expires_in at runtime rather than hard-coding a lifetime (RFC 0004 §4.3).
        var expiresIn = json.TryGetProperty(OAuthProtocol.ExpiresInField, out var exp) && exp.TryGetInt32(out var seconds)
            ? seconds
            : 3600;
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        return new OAuthTokenSet(accessToken, refreshToken, expiresAt, scopes);
    }

    /// <summary>Reads and decrypts the OAuth app credentials from the integration's ConfigJson.</summary>
    private async Task<OAuthCredentials> ResolveCredentialsAsync(Guid integrationId, CancellationToken ct)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");

        var decryptedConfig = IntegrationExtensions.UnprotectSecrets(integrationRegistry.Find(integration.Type)?.Manifest.ConfigType, integration.ConfigJson, secretProtector);
        var obj = JsonSerializer.Deserialize<JsonElement>(decryptedConfig);

        var clientId = obj.TryGetProperty("clientId", out var cid) ? cid.GetString() : null;
        var clientSecret = obj.TryGetProperty("clientSecret", out var cs) ? cs.GetString() : null;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                $"Integration {integrationId} has no OAuth client credentials configured. Enter the Client ID and Client Secret first.");

        return new OAuthCredentials(clientId, clientSecret);
    }

    public Task<string> GetRedirectUriAsync(CancellationToken ct = default) => ResolveRedirectUriAsync(ct);

    private IOAuthProviderDescriptor ResolveDescriptor(string providerId) =>
        _descriptors.TryGetValue(providerId, out var d)
            ? d
            : throw new InvalidOperationException($"Unknown OAuth provider '{providerId}'.");

    private async Task<string> ResolveRedirectUriAsync(CancellationToken ct)
    {
        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = configuration["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "Site URL is not configured. Set it in Configuration → General or via the App__BaseUrl env var.");

        // Points at the admin SPA callback page (which then POSTs code+state back to the API),
        // mirroring how the OIDC sign-in flow lands on /admin/auth/oidc/callback.
        return $"{baseUrl}/admin/integrations/oauth/callback";
    }

    private static string StateKey(string state) => $"oauth:integration:state:{state}";

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private record OAuthStatePayload(string ProviderId, Guid IntegrationId, string CodeVerifier);
    private record OAuthCredentials(string ClientId, string ClientSecret);
}
