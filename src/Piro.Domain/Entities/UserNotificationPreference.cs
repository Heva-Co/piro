namespace Piro.Domain.Entities;

/// <summary>
/// A user's personal notification handle for a specific integration, ordered by priority.
/// When the user is on-call and needs to be notified, preferences are tried in ascending <see cref="Priority"/> order.
/// </summary>
public class UserNotificationPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int IntegrationId { get; set; }
    public Integration Integration { get; set; } = null!;

    /// <summary>Personal handle for this integration: SlackUserId, TelegramChatId, phone number, email address, etc.</summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>Dispatch order — lower value = higher priority (1 fires before 2).</summary>
    public int Priority { get; set; }
}
