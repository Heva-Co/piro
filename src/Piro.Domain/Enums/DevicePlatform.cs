namespace Piro.Domain.Enums;

/// <summary>The mobile platform a registered <see cref="Entities.DeviceToken"/> belongs to, which
/// selects the push transport (FCM for Android, APNs for iOS).</summary>
public enum DevicePlatform
{
    Android = 0,
    Ios = 1,
}
