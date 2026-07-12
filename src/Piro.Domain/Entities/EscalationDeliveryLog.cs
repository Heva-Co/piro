namespace Piro.Domain.Entities;

/// <summary>
/// Records a single on-call notification attempt made by <see cref="EscalationDeliveryLog.Alert"/>'s
/// escalation (see EscalationCheckerService) — one row per (step, on-call user, channel) tried,
/// whether it succeeded or failed. This is the durable history behind what today only exists as
/// application log lines: which step fired, who it tried to reach, how, and whether it actually
/// got through — queryable per-alert instead of grep'd out of server logs.
/// </summary>
public class EscalationDeliveryLog
{
    public int Id { get; set; }
    public int AlertId { get; set; }

    /// <summary>0-based escalation step index this attempt belongs to.</summary>
    public int StepIndex { get; set; }

    public int UserId { get; set; }

    /// <summary>Display name of the on-call user this attempt targeted, frozen at attempt time.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>The integration type used for this attempt (e.g. Telegram, Email, Pushover).</summary>
    public Enums.IntegrationType ChannelType { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>Error message when <see cref="Succeeded"/> is false. Null on success.</summary>
    public string? ErrorMessage { get; set; }

    public DateTimeOffset AttemptedAt { get; set; }

    public Alert Alert { get; set; } = null!;
}
