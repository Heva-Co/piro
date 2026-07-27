using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush.Transport;

/// <summary>
/// Delivers to Android devices via Firebase Cloud Messaging (FCM v1) using the official Firebase Admin
/// SDK. Credentials arrive per-call from the resolved <see cref="MobilePushConfig"/>; a
/// <see cref="FirebaseApp"/> is created once per distinct service account and cached, since the SDK
/// keys its apps by name. A critical page is sent at high priority with an Android notification-channel
/// id the app maps to a DND-bypassing (alarm-category) channel.
/// </summary>
public sealed class FcmPushTransport(ILogger<FcmPushTransport> logger) : IPushTransport
{
    /// <summary>Notification-channel id the Android client registers as an alarm-category channel (bypasses DND).</summary>
    private const string CriticalChannelId = "piro_critical";
    private const string DefaultChannelId = "piro_default";

    private static readonly ConcurrentDictionary<string, FirebaseApp> Apps = new();
    private static readonly object AppsLock = new();

    public DevicePushPlatform Platform => DevicePushPlatform.Android;

    public PushTransportMode Mode => PushTransportMode.Direct;

    public bool IsConfigured(MobilePushConfig config) => !string.IsNullOrWhiteSpace(config.FcmServiceAccountJson);

    public async Task<PushSendResult> SendAsync(string token, PushMessage message, MobilePushConfig config, CancellationToken ct = default)
    {
        if (!IsConfigured(config))
            return PushSendResult.NotConfigured;

        var messaging = FirebaseMessaging.GetMessaging(GetApp(config.FcmServiceAccountJson!));

        // Data-only message (no Notification payload): this guarantees the app's own
        // FirebaseMessagingService.onMessageReceived runs even in the background, so the app controls the
        // channel, posts to the critical channel, and actively rings the alarm. A notification payload
        // would be handled by the system in the background, bypassing that logic.
        var fcmMessage = new Message
        {
            // NOTE: this is the device *registration token* the client obtains from
            // FirebaseMessaging.getToken() — it belongs in Token, NOT Fid. The SDK marks Token
            // "deprecated in favor of Fid", but Fid is the Firebase Installation ID (a different value);
            // sending a registration token as Fid makes FCM reject it as unregistered.
            Token = token,
            Data = BuildData(message),
            Android = new AndroidConfig { Priority = Priority.High },
        };

        try
        {
            await messaging.SendAsync(fcmMessage, ct);
            return PushSendResult.Sent;
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            // Token is dead or malformed — the caller prunes it. Log the exact code/message so a
            // misconfiguration (SenderId mismatch, wrong service account) is distinguishable from a
            // genuinely stale token.
            logger.LogWarning(
                "FCM rejected token (pruning): errorCode={ErrorCode}, messagingCode={MsgCode}, message={Message}",
                ex.ErrorCode, ex.MessagingErrorCode, ex.Message);
            return PushSendResult.Unregistered;
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(
                "FCM transient failure: errorCode={ErrorCode}, messagingCode={MsgCode}, message={Message}",
                ex.ErrorCode, ex.MessagingErrorCode, ex.Message);
            return PushSendResult.TransientFailure;
        }
    }

    private static Dictionary<string, string> BuildData(PushMessage message)
    {
        // When the device published a push public key, send only the sealed envelope: the title, body,
        // event key, alert id and url are all inside it. Keeping any of them alongside in the clear
        // would make the encryption pointless, since the payload travels the same hop either way.
        if (!string.IsNullOrEmpty(message.SealedPayload))
        {
            return new Dictionary<string, string>
            {
                ["ciphertext"] = message.SealedPayload,
                // Priority metadata the client needs before it can decrypt anything.
                ["critical"] = message.Critical ? "true" : "false",
            };
        }

        // Legacy cleartext path, for devices registered before they published a key. They re-register
        // with one on the next app launch, at which point they move to the sealed path above.
        var data = new Dictionary<string, string>
        {
            ["title"] = message.Title,
            ["body"] = message.Body,
            ["eventKey"] = message.EventKey,
            ["alertId"] = message.AlertId.ToString(),
            ["critical"] = message.Critical ? "true" : "false",
        };
        if (!string.IsNullOrWhiteSpace(message.Url)) data["url"] = message.Url;
        return data;
    }

    private static FirebaseApp GetApp(string serviceAccountJson)
    {
        var name = "piro-mobilepush-" + Fingerprint(serviceAccountJson);
        if (Apps.TryGetValue(name, out var cached))
            return cached;

        // FirebaseApp keeps a *global* registry keyed by name, separate from our cache. Under
        // concurrency ConcurrentDictionary.GetOrAdd can run the factory on two threads at once, and the
        // second FirebaseApp.Create(name) throws "already exists". Serialize creation and reuse any
        // instance already registered (ours or the SDK's).
        lock (AppsLock)
        {
            if (Apps.TryGetValue(name, out cached))
                return cached;

            // Reuse the app if it's already in FirebaseApp's global registry; otherwise create it.
            // GetInstance may either return null or throw when the name isn't registered (SDK-version
            // dependent), so handle both: a null/exception means "not there yet, create it".
            FirebaseApp? app = null;
            try
            {
                app = FirebaseApp.GetInstance(name);
            }
            catch (ArgumentException)
            {
                // Not registered — fall through to create.
            }

            if (app is null)
            {
                var credential = CredentialFactory
                    .FromJson<ServiceAccountCredential>(serviceAccountJson)
                    .ToGoogleCredential();
                app = FirebaseApp.Create(new AppOptions { Credential = credential }, name);
            }

            Apps[name] = app;
            return app;
        }
    }

    private static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
