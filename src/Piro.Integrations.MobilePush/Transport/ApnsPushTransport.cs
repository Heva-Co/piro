using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush.Transport;

/// <summary>
/// Delivers to iOS devices via APNs over HTTP/2 with token-based (JWT / .p8) authentication. A
/// critical page sets <c>interruption-level: critical</c> and a critical sound so it bypasses Focus /
/// Do Not Disturb — this requires the app to hold Apple's Critical Alerts entitlement. The provider
/// JWT is signed with ES256 and cached (~50 min) per key, as Apple recommends reusing it rather than
/// minting one per push. The HTTP/2 client is injected so the transport stays free of client wiring.
/// </summary>
public sealed class ApnsPushTransport(HttpClient httpClient) : IPushTransport
{
    private const string ProductionHost = "https://api.push.apple.com";
    private const string SandboxHost = "https://api.sandbox.push.apple.com";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(50);

    private static readonly ConcurrentDictionary<string, CachedToken> TokenCache = new();

    public DevicePushPlatform Platform => DevicePushPlatform.Ios;

    public bool IsConfigured(MobilePushConfig config) =>
        !string.IsNullOrWhiteSpace(config.ApnsPrivateKey) &&
        !string.IsNullOrWhiteSpace(config.ApnsKeyId) &&
        !string.IsNullOrWhiteSpace(config.ApnsTeamId) &&
        !string.IsNullOrWhiteSpace(config.ApnsBundleId);

    public async Task<PushSendResult> SendAsync(string token, PushMessage message, MobilePushConfig config, CancellationToken ct = default)
    {
        if (!IsConfigured(config))
            return PushSendResult.NotConfigured;

        var host = config.ApnsProduction ? ProductionHost : SandboxHost;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/3/device/{token}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(BuildPayload(message), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("authorization", $"bearer {GetProviderToken(config)}");
        request.Headers.TryAddWithoutValidation("apns-topic", config.ApnsBundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Headers.TryAddWithoutValidation("apns-priority", "10");

        HttpResponseMessage response;
        try { response = await httpClient.SendAsync(request, ct); }
        catch (HttpRequestException) { return PushSendResult.TransientFailure; }
        catch (TaskCanceledException) { return PushSendResult.TransientFailure; }

        if (response.IsSuccessStatusCode)
            return PushSendResult.Sent;

        // 410 Gone (token no longer valid) or 400 BadDeviceToken → prune. Everything else is transient.
        if (response.StatusCode == HttpStatusCode.Gone)
            return PushSendResult.Unregistered;
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Contains("BadDeviceToken", StringComparison.Ordinal) ||
                body.Contains("DeviceTokenNotForTopic", StringComparison.Ordinal))
                return PushSendResult.Unregistered;
        }
        return PushSendResult.TransientFailure;
    }

    private static string BuildPayload(PushMessage message)
    {
        object sound = message.Critical
            ? new { critical = 1, name = "default", volume = 1.0 }
            : (object)"default";

        var aps = new Dictionary<string, object?>
        {
            ["alert"] = new { title = message.Title, body = message.Body },
            ["sound"] = sound,
            ["interruption-level"] = message.Critical ? "critical" : "time-sensitive",
        };

        var payload = new Dictionary<string, object?>
        {
            ["aps"] = aps,
            ["eventKey"] = message.EventKey,
        };
        if (!string.IsNullOrWhiteSpace(message.Url)) payload["url"] = message.Url;

        return JsonSerializer.Serialize(payload);
    }

    private static string GetProviderToken(MobilePushConfig config)
    {
        var cacheKey = $"{config.ApnsTeamId}:{config.ApnsKeyId}";
        var now = DateTimeOffset.UtcNow;
        if (TokenCache.TryGetValue(cacheKey, out var cached) && now - cached.IssuedAt < TokenLifetime)
            return cached.Token;

        var jwt = SignJwt(config, now);
        TokenCache[cacheKey] = new CachedToken(jwt, now);
        return jwt;
    }

    private static string SignJwt(MobilePushConfig config, DateTimeOffset issuedAt)
    {
        // APNs provider token: an ES256-signed JWT with {alg,kid} header and {iss,iat} claims. Built by
        // hand (base64url header.payload signed with the .p8 EC key) so the transport needs no JWT library.
        var header = ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string> { ["alg"] = "ES256", ["kid"] = config.ApnsKeyId! }));
        var payload = ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, object> { ["iss"] = config.ApnsTeamId!, ["iat"] = issuedAt.ToUnixTimeSeconds() }));
        var signingInput = $"{header}.{payload}";

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(config.ApnsPrivateKey);
        var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);

        return $"{signingInput}.{ToBase64Url(signature)}";
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var buffer = new char[Base64Url.GetEncodedLength(bytes.Length)];
        Base64Url.EncodeToChars(bytes, buffer);
        return new string(buffer);
    }

    private readonly record struct CachedToken(string Token, DateTimeOffset IssuedAt);
}
