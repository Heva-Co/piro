namespace Piro.Domain.Entities;

public class EscalationPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Minutes of no human activity on the alert (acknowledging it counts as activity) before
    /// escalation resumes from step 1, even if it was paused by an ACK. 0 = disabled — once
    /// acknowledged, escalation stays paused indefinitely until the alert resolves.
    /// </summary>
    public int ReEscalateAfterInactivityMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<EscalationStep> Steps { get; set; } = [];

    /// <summary>Services that use this policy for their alerts' on-call escalation. One policy may serve many services.</summary>
    public ICollection<Service> Services { get; set; } = [];

    /// <summary>Integrations whose orphan alerts (no Service to inherit from) use this policy — see RFC 0001 §4.3.</summary>
    public ICollection<Integration> Integrations { get; set; } = [];
}
