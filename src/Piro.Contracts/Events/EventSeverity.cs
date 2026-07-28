namespace Piro.Contracts;

/// <summary>
/// Severity of a notification event as an integration sees it — a neutral value in the contract
/// layer, decoupled from the core's domain <c>AlertSeverity</c> (RFC 0016). The core maps its own
/// severity onto this at the edge when it builds an <see cref="EventContext"/>, so an integration
/// assembly never references a Piro.Domain enum.
/// </summary>
public enum EventSeverity
{
    // Numbered to line up with the domain's AlertSeverity (Warning = 0, Critical = 1) rather than
    // starting at Info. The two enums stay separate types on purpose, and ToEventSeverity is still the
    // right way to cross between them — but they used to disagree numerically, so anything that copied
    // the integer instead of mapping it turned Critical into Warning. That silently downgraded a
    // critical page: the push went out without `interruption-level: critical`, so it never made a
    // sound or broke through Focus. Aligning the values makes the accidental cast harmless.
    Warning = 0,
    Critical = 1,

    // Info has no AlertSeverity counterpart — it is for events that are not alerts at all (an incident
    // opening or resolving), so it sits outside the shared range.
    Info = 2,
}
