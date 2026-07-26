namespace Piro.Application.Interfaces;

/// <summary>
/// Kicks off escalation for a just-created alert right away, instead of waiting for the next tick
/// of the every-minute escalation job. Implementations run the work fire-and-forget on their own
/// scope, so the alert-creation request is never blocked by notification I/O.
/// </summary>
public interface IImmediateEscalationTrigger
{
    /// <summary>Fire escalation for the given alert id in the background. Safe to call unconditionally;
    /// the escalation checker no-ops when the alert has no snapshotted escalation policy.</summary>
    void Trigger(int alertId);
}
