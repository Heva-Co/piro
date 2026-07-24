using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.GoogleCloud;

/// <summary>
/// Obtains and caches OAuth2 access tokens for Google Cloud service accounts.
/// Resolves the integration instance's decrypted <see cref="GoogleCloudConfig"/> through the host,
/// parses the service account JSON, builds an RS256-signed JWT, and exchanges it for a bearer token via
/// the Google token endpoint. The caller (e.g. the Cloud Run Job check) supplies only the instance id.
/// </summary>
public class GcpTokenProvider(
    IHttpClientFactory httpClientFactory,
    GcpTokenCache tokenCache,
    IIntegrationHost integrationHost) : IGcpTokenProvider
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> GetAccessTokenAsync(Guid integrationInstanceId, CancellationToken ct = default)
    {
        if (tokenCache.TryGet(integrationInstanceId, out var cached))
            return cached;

        var config = await integrationHost.GetConfigAsync<GoogleCloudConfig>(integrationInstanceId, ct)
            ?? throw new InvalidOperationException($"GoogleCloud integration {integrationInstanceId} not found or unconfigured.");

        var sa = JsonSerializer.Deserialize<ServiceAccountJson>(config.ServiceAccountJson, _json)
            ?? throw new InvalidOperationException("Invalid service account JSON inside integration config.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiry = now + 3600;

        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = sa.ClientEmail,
            scope = "https://www.googleapis.com/auth/cloud-platform",
            aud = "https://oauth2.googleapis.com/token",
            iat = now,
            exp = expiry
        }));

        var signingInput = $"{header}.{payload}";

        var privateKeyPem = sa.PrivateKey
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
            .Replace("-----END RSA PRIVATE KEY-----", "")
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyPem), out _);

        var signatureBytes = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var jwt = $"{signingInput}.{Base64UrlEncode(signatureBytes)}";

        var client = httpClientFactory.CreateClient("piro-http");
        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent([
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("assertion", jwt)
            ]),
            ct);

        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);
        var tokenData = JsonSerializer.Deserialize<TokenResponse>(tokenJson, _json)
            ?? throw new InvalidOperationException("Empty token response from Google.");

        var cacheExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn - 300);
        tokenCache.Set(integrationInstanceId, tokenData.AccessToken, cacheExpiry);
        return tokenData.AccessToken;
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private record ServiceAccountJson(
        [property: JsonPropertyName("client_email")] string ClientEmail,
        [property: JsonPropertyName("private_key")] string PrivateKey);

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
