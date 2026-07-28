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

    public PushTransportMode Mode => PushTransportMode.Direct;

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
            Content = new StringContent(BuildPayload(message, config), Encoding.UTF8, "application/json"),
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

    private static string BuildPayload(PushMessage message, MobilePushConfig config)
    {
        // `critical: 1` and `interruption-level: critical` both require Apple to have granted the app
        // the Critical Alerts entitlement, which is requested through a separate form and approved
        // case by case. Sending them without it does not degrade to a quiet notification: APNs rejects
        // the push and nothing is delivered, which for a page is the worst outcome available. So the
        // operator states whether the entitlement was granted, and until then a critical alert still
        // gets time-sensitive — through Focus, though not through the silent switch.
        var canBypassSilent = message.Critical && config.ApnsCriticalAlerts;

        object sound = canBypassSilent
            ? new { critical = 1, name = "default", volume = 1.0 }
            : (object)"default";

        var aps = new Dictionary<string, object?>
        {
            ["sound"] = sound,
            ["interruption-level"] = canBypassSilent ? "critical" : "time-sensitive",
        };

        var payload = new Dictionary<string, object?> { ["aps"] = aps };

        // When the device published a push public key, send only the sealed envelope: the title, body,
        // event key, alert id and url are all inside it. Keeping any of them alongside in the clear
        // would make the encryption pointless, since the payload travels the same hop either way.
        if (!string.IsNullOrEmpty(message.SealedPayload))
        {
            // mutable-content is what lets the app's Notification Service Extension rewrite the
            // notification before it is shown. Without it iOS displays the placeholder as-is and the
            // ciphertext never gets decrypted.
            aps["mutable-content"] = 1;

            // A placeholder is required: APNs will not display a notification with no alert body, and
            // it is what the user sees for the fraction of a second before the extension replaces it —
            // or permanently, if the extension is killed for running over its time budget.
            aps["alert"] = new { title = "Piro", body = "New alert" };

            payload["ciphertext"] = message.SealedPayload;
            return JsonSerializer.Serialize(payload);
        }

        // Legacy cleartext path, for devices registered before they published a key. They re-register
        // with one on the next app launch, at which point they move to the sealed path above.
        aps["alert"] = new { title = message.Title, body = message.Body };
        payload["eventKey"] = message.EventKey;
        if (message.AlertId != 0) payload["alertId"] = message.AlertId;
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
