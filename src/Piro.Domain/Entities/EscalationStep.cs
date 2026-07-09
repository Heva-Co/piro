namespace Piro.Domain.Entities;

public class EscalationStep
{
    public int Id { get; set; }
    public int PolicyId { get; set; }
    public EscalationPolicy Policy { get; set; } = null!;
    public int Order { get; set; }
    /// <summary>
    /// For step 0: minutes from incident.CreatedAt before first notification.
    /// For steps 1+: minutes from previous step's dispatch time.
    /// </summary>
    public int DelayMinutes { get; set; }
    public int ScheduleId { get; set; }
    public OnCallSchedule Schedule { get; set; } = null!;
}
