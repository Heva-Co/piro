namespace Piro.Domain.Entities;

/// <summary>Junction between an <see cref="AlertConfig"/> and a <see cref="NotificationChannel"/>.</summary>
public class AlertConfigNotificationChannel
{
    public int AlertConfigId { get; set; }
    public int NotificationChannelId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AlertConfig AlertConfig { get; set; } = null!;
    public NotificationChannel NotificationChannel { get; set; } = null!;
}
