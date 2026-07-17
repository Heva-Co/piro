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

    /// <summary>
    /// How many times this step notifies its on-call users before escalation advances to the next
    /// step (or, on the last step, marks the alert Exhausted). Attempts are spaced by
    /// <see cref="RetryIntervalMinutes"/>. 1 = notify once then advance — today's fire-once behavior.
    /// </summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>
    /// Minutes between two attempts of THIS step. Distinct from <see cref="DelayMinutes"/> (which is
    /// the wait BEFORE the step starts). Ignored when <see cref="MaxRetries"/> == 1. 0 = each tick may
    /// attempt (bounded only by the job's 1-minute cadence).
    /// </summary>
    public int RetryIntervalMinutes { get; set; }

    public int ScheduleId { get; set; }
    public OnCallSchedule Schedule { get; set; } = null!;
}
