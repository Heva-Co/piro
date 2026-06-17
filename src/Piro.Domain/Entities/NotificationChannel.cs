using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A notification channel configuration (email, webhook, Slack, etc.).</summary>
public class NotificationChannel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationChannelType Type { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }

    /// <summary>JSON blob with type-specific configuration (SMTP host, webhook URL, Slack token, etc.).</summary>
    public string MetaJson { get; set; } = "{}";

    /// <summary>When true, automatically added to all alert configs (existing and future).</summary>
    public bool IsGlobal { get; set; }

    /// <summary>When true, cannot be removed from any alert config by users.</summary>
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AlertConfigNotificationChannel> AlertConfigNotificationChannels { get; set; } = [];
}
