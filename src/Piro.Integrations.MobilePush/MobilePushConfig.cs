using System.ComponentModel.DataAnnotations;
using Piro.Contracts;
using Piro.Integrations.MobilePush.Transport;

namespace Piro.Integrations.MobilePush;

/// <summary>
/// Credentials for the MobilePush integration (RFC 0016): the Firebase service account used to send
/// to Android devices (FCM) and the Apple auth key used to send to iOS devices (APNs). All secret
/// material is marked <see cref="SecretField"/> so it is encrypted at rest. A deployment may configure
/// only one platform; the transport for a missing platform simply no-ops.
/// </summary>
public sealed class MobilePushConfig
{
    // --- Delivery mode ---

    /// <summary>
    /// Which transport delivers. Defaults to <see cref="PushTransportMode.Direct"/> so an existing
    /// deployment that already has FCM/APNs credentials keeps behaving exactly as before after an
    /// upgrade — switching to Relay is an explicit opt-in, never inferred from which fields are filled.
    /// </summary>
    [ConfigField("Delivery mode",
        HelpText = "Direct sends with your own FCM/APNs credentials — for an app you built and signed yourself. " +
                   "Relay sends through Heva's push relay, which is what the App Store / Play Store builds of Piro require."
    )]
    public PushTransportMode Mode { get; set; } = PushTransportMode.Direct;

    // --- Heva push relay (Mode = Relay) ---

    [ConfigField("Relay push URL",
        Placeholder = "https://api.dev.heva.pro/socket.io/v1/push",
        HelpText = "Full URL of the relay's push endpoint, including any path prefix it is mounted under."
    )]
    public string? RelayPushUrl { get; set; }

    /// <summary>
    /// Either an issued key (<c>hvr_…</c>), or a single-use invite (<c>inv_…</c>) that Piro redeems once
    /// on save and replaces with the issued key.
    /// </summary>
    [SecretField]
    [ConfigField("Relay API key or invite code",
        Placeholder = "hvr_… or inv_…",
        HelpText = "Paste the hvr_ key Heva issued you, or an inv_ invite code and Piro will redeem it for a key. " +
                   "An invite can only be redeemed once."
    )]
    public string? RelayApiKey { get; set; }

    /// <summary>
    /// Set from the relay's response when a key is issued, never typed by the operator: the key is scoped
    /// to exactly one app and the relay rejects a mismatched appId with 403. Stored so a failure can name
    /// which app the key is for without revealing the key.
    /// </summary>
    [ConfigField("Relay app ID",
        HelpText = "Filled in automatically from the relay when the key is issued."
    )]
    public string? RelayAppId { get; set; }

    /// <summary>Set from the relay's response, for support: identifies the key without exposing it.</summary>
    [ConfigField("Relay key ID",
        HelpText = "Filled in automatically from the relay when the key is issued."
    )]
    public string? RelayKeyId { get; set; }

    // --- Android / FCM ---

    [VisibleWhen("mode", "Direct")]
    [SecretField]
    [ConfigField("FCM service account JSON",
        HelpText = "The Firebase service account key (JSON) with the Cloud Messaging role. Used to send to Android devices."
    )]
    public string? FcmServiceAccountJson { get; set; }

    // --- iOS / APNs ---

    [VisibleWhen("mode", "Direct")]
    [SecretField]
    [ConfigField("APNs auth key (.p8)",
        HelpText = "Contents of the Apple .p8 token-signing key. Used to send to iOS devices."
    )]
    public string? ApnsPrivateKey { get; set; }

    [VisibleWhen("mode", "Direct")]
    [ConfigField("APNs Key ID", Placeholder = "e.g. ABC123DEFG")]
    public string? ApnsKeyId { get; set; }

    [VisibleWhen("mode", "Direct")]
    [ConfigField("Apple Team ID", Placeholder = "e.g. DEF456GHIJ")]
    public string? ApnsTeamId { get; set; }

    [VisibleWhen("mode", "Direct")]
    [ConfigField("App bundle ID", Placeholder = "e.g. co.heva.piro")]
    public string? ApnsBundleId { get; set; }

    [VisibleWhen("mode", "Direct")]
    [ConfigField("Use APNs production server",
        HelpText = "On for TestFlight/App Store builds; off for development (sandbox) builds."
    )]
    public bool ApnsProduction { get; set; } = true;

    [VisibleWhen("mode", "Direct")]
    [ConfigField("App has Apple's Critical Alerts entitlement",
        HelpText = "Only turn this on once Apple has approved com.apple.developer.usernotifications" +
                   ".critical-alerts for the app. A critical page then bypasses silent mode and Focus. " +
                   "With it on but the entitlement missing, APNs rejects the push outright and the page " +
                   "is not delivered at all — worse than arriving quietly."
    )]
    public bool ApnsCriticalAlerts { get; set; }
}
