using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A notification channel configuration (email, webhook, Slack, etc.).</summary>
public class NotificationChannel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IntegrationType Type { get; set; }
    public string? Description { get; set; }
    /// <summary>When true, this channel is disabled and will not send notifications.</summary>
    public bool IsInactive { get; set; }

    /// <summary>Optional integration that provides the credentials for this channel.</summary>
    public int? IntegrationId { get; set; }
    public Integration? Integration { get; set; }

    /// <summary>JSON blob with channel-specific overrides (target address, body template, etc.).</summary>
    public string MetaJson { get; set; } = "{}";

    /// <summary>When true, automatically added to all alert configs (existing and future).</summary>
    public bool IsGlobal { get; set; }

    /// <summary>When true, cannot be removed from any alert config by users.</summary>
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AlertConfigNotificationChannel> AlertConfigNotificationChannels { get; set; } = [];
}
