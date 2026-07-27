using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Piro.Integrations.MobilePush.Relay;

/// <summary>
/// Exchanges a single-use invite code for a scoped relay API key.
///
/// Heva mints the invite with an admin token that never leaves Heva; the operator only ever handles the
/// <c>inv_</c> code. The relay decides which app the resulting key is scoped to from the invite itself, so
/// redeeming a code can never grant access to an app Heva did not intend — that is why there is no appId
/// in the request.
///
/// A redemption is attempted exactly once. The code is spent on success, so retrying would fail against a
/// code that was already consumed, and reporting that as a bad credential would send the operator looking
/// for a problem they do not have.
/// </summary>
public sealed class RelayInviteRedeemer(HttpClient httpClient, ILogger<RelayInviteRedeemer> logger)
{
    /// <summary>Prefix of a single-use invite code, as issued by the relay.</summary>
    public const string InvitePrefix = "inv_";

    /// <summary>Prefix of an issued API key.</summary>
    public const string ApiKeyPrefix = "hvr_";

    /// <summary>True when <paramref name="value"/> looks like an invite that still needs redeeming.</summary>
    public static bool LooksLikeInvite(string? value) =>
        value?.StartsWith(InvitePrefix, StringComparison.Ordinal) == true;

    /// <summary>True when <paramref name="value"/> is already an issued key.</summary>
    public static bool LooksLikeApiKey(string? value) =>
        value?.StartsWith(ApiKeyPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Redeems <paramref name="inviteCode"/> against the relay's register endpoint, which is derived from
    /// the configured push URL so the operator only ever supplies one address.
    /// </summary>
    /// <param name="pushUrl">The configured push endpoint, e.g. https://host/v1/push.</param>
    /// <param name="inviteCode">The <c>inv_…</c> code.</param>
    /// <param name="caller">A human-readable label the relay records against the issued key.</param>
    public async Task<RelayRedeemResult> RedeemAsync(
        string pushUrl,
        string inviteCode,
        string caller,
        CancellationToken ct = default)
    {
        if (!TryResolveRegisterUrl(pushUrl, out var registerUrl))
        {
            return RelayRedeemResult.Failed(
                $"'{pushUrl}' does not look like a relay push URL. It should end in /v1/push.");
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(
                registerUrl,
                new RegisterRequest { InviteCode = inviteCode, Caller = caller },
                ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not reach the relay at {Url} to redeem an invite.", registerUrl);
            return RelayRedeemResult.Failed(
                "Could not reach the relay. Check the URL and that the relay is up, then try again — " +
                "the invite has not been used.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return RelayRedeemResult.Failed(
                "The relay did not respond in time. The invite may or may not have been consumed; " +
                "ask Heva to confirm before retrying.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Created || response.IsSuccessStatusCode)
            {
                var issued = await response.Content.ReadFromJsonAsync<RegisterResponse>(ct);
                if (issued is null || string.IsNullOrWhiteSpace(issued.ApiKey))
                {
                    return RelayRedeemResult.Failed(
                        "The relay accepted the invite but returned no API key. Contact Heva — the invite " +
                        "may have been consumed.");
                }

                logger.LogInformation(
                    "Redeemed a relay invite: key {KeyId} scoped to app {AppId}.", issued.KeyId, issued.AppId);

                return RelayRedeemResult.Succeeded(issued.ApiKey, issued.AppId, issued.KeyId);
            }

            // 401 here means the invite specifically, not our identity: this endpoint is what issues
            // credentials, so there is no key to be wrong yet.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return RelayRedeemResult.Failed(
                    "The relay rejected this invite code as invalid, expired, or already used. " +
                    "Ask Heva for a new one.");
            }

            var body = await SafeBodyAsync(response, ct);
            logger.LogWarning(
                "Relay invite redemption failed with {Status}: {Body}", (int)response.StatusCode, body);

            return RelayRedeemResult.Failed(
                $"The relay rejected the redemption ({(int)response.StatusCode}). {body}".TrimEnd());
        }
    }

    /// <summary>
    /// Derives the register endpoint from the push endpoint. They share a mount point, so asking the
    /// operator for both would be two chances to get one address wrong.
    /// </summary>
    internal static bool TryResolveRegisterUrl(string? pushUrl, out string registerUrl)
    {
        registerUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(pushUrl)) return false;
        if (!Uri.TryCreate(pushUrl.Trim(), UriKind.Absolute, out var uri)) return false;

        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/push", StringComparison.OrdinalIgnoreCase)) return false;

        var registerPath = string.Concat(path.AsSpan(0, path.Length - "/push".Length), "/register");
        registerUrl = new UriBuilder(uri) { Path = registerPath, Query = string.Empty }.Uri.ToString();
        return true;
    }

    private static async Task<string> SafeBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length > 300 ? body[..300] : body;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private sealed class RegisterRequest
    {
        [JsonPropertyName("inviteCode")] public required string InviteCode { get; init; }
        [JsonPropertyName("caller")] public required string Caller { get; init; }
    }

    private sealed class RegisterResponse
    {
        [JsonPropertyName("keyId")] public string? KeyId { get; init; }
        [JsonPropertyName("appId")] public string? AppId { get; init; }
        [JsonPropertyName("apiKey")] public string? ApiKey { get; init; }
    }
}

/// <summary>Outcome of a redemption. The error is operator-facing, so it says what to do next.</summary>
public sealed record RelayRedeemResult
{
    public bool Success { get; private init; }
    public string? ApiKey { get; private init; }
    public string? AppId { get; private init; }
    public string? KeyId { get; private init; }
    public string? Error { get; private init; }

    public static RelayRedeemResult Succeeded(string apiKey, string? appId, string? keyId) => new()
    {
        Success = true,
        ApiKey = apiKey,
        AppId = appId,
        KeyId = keyId,
    };

    public static RelayRedeemResult Failed(string error) => new() { Success = false, Error = error };
}
