namespace Piro.Domain.Entities;

/// <summary>Junction between an <see cref="AlertConfig"/> and a notification <see cref="Trigger"/>.</summary>
public class AlertConfigTrigger
{
    public int AlertConfigId { get; set; }
    public int TriggerId { get; set; }
    public DateTime CreatedAt { get; set; }

    public AlertConfig AlertConfig { get; set; } = null!;
    public Trigger Trigger { get; set; } = null!;
}
