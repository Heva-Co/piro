namespace Piro.Domain.Enums;

/// <summary>
/// The delivery contract a notification subscription targets (RFC 0009 §4.1, §4.4). Derived from a
/// provider's declared capabilities, not chosen freely — a provider that only sends personal
/// notifications can only back a <see cref="Personal"/> subscription, and so on.
/// </summary>
public enum NotificationTargetKind
{
    /// <summary>Reaches one person at their own handle (email, chat id, phone) — mode 1.</summary>
    Personal,

    /// <summary>Posts to a shared team channel/space/room/topic — mode 2.</summary>
    Channel,

    /// <summary>Hands the event to an integration that decides what to do (e.g. PagerDuty) — mode 3.</summary>
    Integration,
}
