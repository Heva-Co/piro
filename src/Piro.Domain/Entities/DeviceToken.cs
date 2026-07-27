using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A push-notification token for one of a user's mobile devices (the Piro on-call app). A user can
/// register many devices; when the user is paged, the MobilePush channel fans out the notification
/// to <em>all</em> of them at once. The token is the platform's registration handle — an FCM
/// registration token on Android or an APNs device token on iOS — and is refreshed by the client as
/// the platform rotates it.
/// <para>
/// This is deliberately separate from <see cref="UserNotificationPreference"/>: a user has a single
/// "MobilePush" preference (so escalation treats it as one destination), and that one preference
/// fans out to every row here. Registering or removing a device is plain CRUD on this table and is
/// transparent to the escalation engine.
/// </para>
/// </summary>
public class DeviceToken
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Which platform this token targets — selects FCM (Android) vs APNs (iOS) at send time.</summary>
    public DevicePlatform Platform { get; set; }

    /// <summary>The platform push token: an FCM registration token or an APNs device token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Optional human label for the device (e.g. "Pixel 8", "iPhone 15"), shown when listing devices.</summary>
    public string? DeviceName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Refreshed every time the client re-registers the same token, so stale devices can be pruned.</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Consecutive delivery failures. A token the provider reports as unregistered is pruned; transient
    /// failures increment this so a persistently failing token can be cleaned up without losing the rest.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// The device's push public key: base64url of an uncompressed P-256 point (65 bytes). The private
    /// half never leaves the device, so a payload sealed against this can only be read there. Null for a
    /// device registered before push encryption shipped; it publishes one on its next app launch.
    /// </summary>
    public string? PushPublicKey { get; set; }
}
