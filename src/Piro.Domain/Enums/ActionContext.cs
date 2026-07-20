namespace Piro.Domain.Enums;

/// <summary>
/// The kind of local object an <c>IIntegrationAction</c> applies to — the discriminator that drives
/// which detail pages surface an action's button (RFC 0012 §4.2). The backend classifies actions by
/// this; the frontend decides where on the page the button goes. Closed to these three in Phase 1;
/// adding a fourth (e.g. <c>Service</c>) is a one-value addition here plus a container placement on
/// that page.
/// <para>
/// Also the polymorphic target discriminator persisted on <c>ExternalReference.TargetType</c> — the
/// outbound link "Piro created external X for this object" points at (<see cref="ActionContext"/>,
/// TargetId).
/// </para>
/// </summary>
public enum ActionContext
{
    Alert = 0,
    Incident = 1,
    Maintenance = 2,
}
