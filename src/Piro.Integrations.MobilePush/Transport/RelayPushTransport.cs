using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush.Transport;

/// <summary>
/// Delivers through the Heva push relay, for deployments whose users installed the store-published Piro
/// app. That app is signed against Heva's Firebase project and Apple bundle id, so a self-hosted backend
/// has no credentials that can reach it; the relay holds them centrally and forwards.
///
/// The relay is a blind forwarder: it receives only routing fields plus an opaque ciphertext, and it
/// requires that ciphertext — there is no cleartext mode. So this transport refuses to send when the
/// dispatcher could not seal for the device, rather than falling back to plaintext.
///
/// One instance per platform; the platform is a constructor argument because the wire contract differs
/// only in the string sent.
/// </summary>
public sealed class RelayPushTransport(
    DevicePushPlatform platform,
    HttpClient httpClient,
    ILogger<RelayPushTransport> logger) : IPushTransport
{
    public DevicePushPlatform Platform => platform;

    public PushTransportMode Mode => PushTransportMode.Relay;

    public bool IsConfigured(MobilePushConfig config) =>
        !string.IsNullOrWhiteSpace(config.RelayPushUrl)
        && !string.IsNullOrWhiteSpace(config.RelayApiKey)
        && !string.IsNullOrWhiteSpace(config.RelayAppId);

    public async Task<PushSendResult> SendAsync(
        string token,
        PushMessage message,
        MobilePushConfig config,
        CancellationToken ct = default)
    {
        if (!IsConfigured(config))
            return PushSendResult.NotConfigured;

        // The relay rejects an empty ciphertext with 400, and sending the alert in the clear would defeat
        // the entire point of routing through a third party. A device with no published public key simply
        // cannot be reached this way until it re-registers.
        if (string.IsNullOrEmpty(message.SealedPayload))
        {
            logger.LogWarning(
                "Relay push skipped for {Platform} token {TokenPrefix}: no sealed payload (device has no push public key).",
                platform, Truncate(token));
            return PushSendResult.NotConfigured;
        }

        var request = new RelayPushRequest
        {
            AppId = config.RelayAppId!,
            // Canonical spelling per the relay contract: "Android" / "iOS". It is case-sensitive, and
            // the legacy "Ios" is only tolerated for older callers.
            Platform = platform == DevicePushPlatform.Ios ? "iOS" : "Android",
            Token = token,
            Critical = message.Critical,
            Ciphertext = message.SealedPayload,
        };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.RelayPushUrl)
            {
                Content = JsonContent.Create(request),
            };
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {config.RelayApiKey}");

            response = await httpClient.SendAsync(httpRequest, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Relay push failed to reach {Url}.", config.RelayPushUrl);
            return PushSendResult.TransientFailure;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Relay push to {Url} timed out.", config.RelayPushUrl);
            return PushSendResult.TransientFailure;
        }

        using (response)
        {
            return await ClassifyAsync(response, token, ct);
        }
    }

    /// <summary>
    /// Maps the relay's HTTP status onto a send result. The load-bearing rule: <b>only 410 prunes</b>.
    /// The relay deliberately collapses every fault of its own — expired APNs key, missing FCM
    /// credential, unconfigured APNs, unknown appId, its own database being down — into 503 precisely so
    /// callers do not delete healthy device tokens over someone else's misconfiguration. Treating 401,
    /// 403 or 429 as token death would do exactly that damage, irreversibly.
    /// </summary>
    private async Task<PushSendResult> ClassifyAsync(
        HttpResponseMessage response,
        string token,
        CancellationToken ct)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                return PushSendResult.Sent;

            case HttpStatusCode.Gone:
                logger.LogInformation(
                    "Relay reported {Platform} token {TokenPrefix} as unregistered; pruning.",
                    platform, Truncate(token));
                return PushSendResult.Unregistered;

            case HttpStatusCode.ServiceUnavailable:
                // Either a genuine provider blip or a relay-side misconfiguration. Both mean retry
                // later and keep the token; the relay operator sees the real reason in their metrics.
                logger.LogWarning(
                    "Relay reported a transient failure for {Platform} token {TokenPrefix}: {Body}",
                    platform, Truncate(token), await SafeBodyAsync(response, ct));
                return PushSendResult.TransientFailure;

            case HttpStatusCode.Unauthorized:
                logger.LogError(
                    "Relay rejected our API key (401). The key may be revoked or wrong — push will keep " +
                    "failing until it is replaced. Tokens are kept. Body: {Body}",
                    await SafeBodyAsync(response, ct));
                return PushSendResult.TransientFailure;

            case HttpStatusCode.Forbidden:
                logger.LogError(
                    "Relay says our key is not scoped to the configured appId (403). Tokens are kept. Body: {Body}",
                    await SafeBodyAsync(response, ct));
                return PushSendResult.TransientFailure;

            case HttpStatusCode.TooManyRequests:
                logger.LogWarning(
                    "Relay rate-limited us (429), retry-after={RetryAfter}s. Tokens are kept.",
                    response.Headers.RetryAfter?.Delta?.TotalSeconds);
                return PushSendResult.TransientFailure;

            case HttpStatusCode.BadRequest:
                // Our request is malformed — a bug on this side, not a dead token.
                logger.LogError(
                    "Relay rejected our request as invalid (400): {Body}",
                    await SafeBodyAsync(response, ct));
                return PushSendResult.TransientFailure;

            default:
                logger.LogWarning(
                    "Relay returned an unexpected {Status} for {Platform} token {TokenPrefix}: {Body}",
                    (int)response.StatusCode, platform, Truncate(token), await SafeBodyAsync(response, ct));
                return PushSendResult.TransientFailure;
        }
    }

    private static async Task<string> SafeBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length > 500 ? body[..500] : body;
        }
        catch (Exception)
        {
            return "<unreadable>";
        }
    }

    /// <summary>Device tokens are credentials of a sort; never log one whole.</summary>
    private static string Truncate(string token) =>
        token.Length <= 8 ? token : token[..8] + "…";

    private sealed class RelayPushRequest
    {
        [JsonPropertyName("appId")] public required string AppId { get; init; }
        [JsonPropertyName("platform")] public required string Platform { get; init; }
        [JsonPropertyName("token")] public required string Token { get; init; }
        [JsonPropertyName("critical")] public required bool Critical { get; init; }
        [JsonPropertyName("ciphertext")] public required string Ciphertext { get; init; }
    }
}
