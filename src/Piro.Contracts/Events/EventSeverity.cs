namespace Piro.Contracts;

/// <summary>
/// Severity of a notification event as an integration sees it — a neutral value in the contract
/// layer, decoupled from the core's domain <c>AlertSeverity</c> (RFC 0016). The core maps its own
/// severity onto this at the edge when it builds an <see cref="EventContext"/>, so an integration
/// assembly never references a Piro.Domain enum.
/// </summary>
public enum EventSeverity
{
    Info,
    Warning,
    Critical,
}
