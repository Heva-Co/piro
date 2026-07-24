namespace Piro.Domain.Entities;

/// <summary>
/// A user's personal notification destination, ordered by priority. When the user is on-call and needs
/// to be notified, preferences are tried in ascending <see cref="Priority"/> order.
/// <para>
/// A preference is identified by the integration instance it delivers through
/// (<see cref="IntegrationInstanceId"/>) — e.g. a specific Telegram/Twilio/Ntfy integration — from which
/// its type is read. The one exception is the account-email fallback (<see cref="IsAccountFallback"/>):
/// it has no integration instance because it sends to the user's own account address, so its type is
/// implicitly "Email". There is no separate channel-type column; the type is always derived.
/// </para>
/// </summary>
public class UserNotificationPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// The configured integration instance this preference delivers through — its type (Telegram, Twilio,
    /// Ntfy, …) is read from that integration. Null only for the account-email fallback
    /// (<see cref="IsAccountFallback"/>), which needs no instance.
    /// </summary>
    public Guid? IntegrationInstanceId { get; set; }
    public Integration? Integration { get; set; }

    /// <summary>Personal handle for this destination: Telegram chat id, phone number, email address, etc.</summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>Dispatch order — lower value = higher priority (1 fires before 2).</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Null until the user confirms a one-time code sent to <see cref="Handle"/> — see
    /// UserManagementService.SendVerificationCodeAsync/ConfirmVerificationCodeAsync. Escalation
    /// dispatch skips unverified preferences so a typo'd handle never silently eats a real page.
    /// Reset to null whenever Handle changes.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>
    /// True for the one auto-created Email preference every user gets, mirroring their account
    /// email (AppUser.Email) — always present, reorderable like any other preference, but never
    /// deletable and never hand-edited: its Handle is kept in sync automatically whenever the
    /// account email changes (see UserManagementService.UpdateProfileAsync). This is the only
    /// preference with no <see cref="IntegrationInstanceId"/>.
    /// </summary>
    public bool IsAccountFallback { get; set; }

    /// <summary>
    /// The integration id (RFC 0016) this preference delivers through — "Email" for the account
    /// fallback, otherwise the type of the referenced integration instance. Derived, not stored; the
    /// <see cref="Integration"/> navigation must be loaded for a non-fallback preference. Used to resolve
    /// the right event handler / verification-code sender.
    /// </summary>
    public string ResolveIntegrationId() =>
        IsAccountFallback
            ? "Email"
            : Integration?.Type
                ?? throw new InvalidOperationException(
                    $"Notification preference {Id} has no integration instance loaded and is not the account fallback.");
}
