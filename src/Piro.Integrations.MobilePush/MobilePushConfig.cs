using System.ComponentModel.DataAnnotations;
using Piro.Contracts;

namespace Piro.Integrations.MobilePush;

/// <summary>
/// Credentials for the MobilePush integration (RFC 0016): the Firebase service account used to send
/// to Android devices (FCM) and the Apple auth key used to send to iOS devices (APNs). All secret
/// material is marked <see cref="SecretField"/> so it is encrypted at rest. A deployment may configure
/// only one platform; the transport for a missing platform simply no-ops.
/// </summary>
public sealed class MobilePushConfig
{
    // --- Android / FCM ---

    [SecretField]
    [ConfigField("FCM service account JSON",
        HelpText = "The Firebase service account key (JSON) with the Cloud Messaging role. Used to send to Android devices."
    )]
    public string? FcmServiceAccountJson { get; set; }

    // --- iOS / APNs ---

    [SecretField]
    [ConfigField("APNs auth key (.p8)",
        HelpText = "Contents of the Apple .p8 token-signing key. Used to send to iOS devices."
    )]
    public string? ApnsPrivateKey { get; set; }

    [ConfigField("APNs Key ID", Placeholder = "e.g. ABC123DEFG")]
    public string? ApnsKeyId { get; set; }

    [ConfigField("Apple Team ID", Placeholder = "e.g. DEF456GHIJ")]
    public string? ApnsTeamId { get; set; }

    [ConfigField("App bundle ID", Placeholder = "e.g. co.heva.piro")]
    public string? ApnsBundleId { get; set; }

    [ConfigField("Use APNs production server",
        HelpText = "On for TestFlight/App Store builds; off for development (sandbox) builds."
    )]
    public bool ApnsProduction { get; set; } = true;
}
