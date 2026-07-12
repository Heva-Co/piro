using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// A user's personal notification handle for a specific channel, ordered by priority.
/// When the user is on-call and needs to be notified, preferences are tried in ascending <see cref="Priority"/> order.
/// </summary>
public class UserNotificationPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// The personal channel this preference dispatches through. See
    /// <c>PersonalNotificationChannelExtensions.RequiresIntegration</c> — Email is self-sufficient
    /// from <see cref="Handle"/> alone; Telegram/TwilioSms need shared platform credentials, so
    /// <see cref="IntegrationId"/> must also be set for those.
    /// </summary>
    public PersonalNotificationChannel Channel { get; set; }

    /// <summary>Required when <see cref="Channel"/> needs platform credentials — the Integration providing them.</summary>
    public int? IntegrationId { get; set; }
    public Integration? Integration { get; set; }

    /// <summary>Personal handle for this channel: TelegramChatId, phone number, email address, etc.</summary>
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
    /// account email changes (see UserManagementService.UpdateProfileAsync).
    /// </summary>
    public bool IsAccountFallback { get; set; }
}
