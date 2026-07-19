using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// One notification routing rule the admin configures (RFC 0009 §4.4) — the single, unified concept
/// for reaching a person, a team channel, or an integration. It says: for these catalog
/// <see cref="Events"/>, when the payload passes <see cref="MinSeverity"/> (and later a tag filter),
/// deliver to the destination identified by <see cref="TargetKind"/>. The integration declares which
/// events it <em>can</em> handle; this row is the admin activating which actually fire.
/// </summary>
public class NotificationSubscription
{
    public Guid Id { get; set; }

    /// <summary>Human label, e.g. "Prod alerts → PagerDuty".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The catalog events this subscription fires on, stored as their wire names (a JSON string array).
    /// A subset of the destination's declared <c>SupportedEvents</c>.
    /// </summary>
    public string EventsJson { get; set; } = "[]";

    /// <summary>
    /// Gate: the event's severity must be at least this to fire. Applies to alert events (which carry a
    /// severity); events with no severity are not gated by it. Tag filtering is added in a later phase.
    /// </summary>
    public AlertSeverity MinSeverity { get; set; } = AlertSeverity.Warning;

    /// <summary>Which delivery contract this subscription routes through — derived from the chosen destination.</summary>
    public NotificationTargetKind TargetKind { get; set; }

    /// <summary>Personal destination: the user to reach. Set when <see cref="TargetKind"/> is Personal.</summary>
    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>Group/Integration destination: the integration carrying credentials. Set for Group/Integration.</summary>
    public Guid? IntegrationId { get; set; }
    public Integration? Integration { get; set; }

    /// <summary>Room/space/topic within the integration, when it doesn't self-address (e.g. a Telegram group id).</summary>
    public string? Target { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
