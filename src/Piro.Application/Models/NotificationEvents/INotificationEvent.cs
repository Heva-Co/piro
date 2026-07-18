namespace Piro.Application.Models.NotificationEvents;

/// <summary>
/// A notification event payload — a fact Piro emits, as a stable, self-contained contract (RFC 0009
/// §4.3). It is a complete flat snapshot built by the publisher at emit time, never a live domain
/// entity and never a bare id a subscriber has to resolve: a webhook or future plugin consumes this
/// object without touching Piro's database.
/// <para>
/// The contract evolves <b>additively only</b> — fields are added (optional, with a safe default),
/// never renamed, retyped, or removed except via an <c>[Obsolete]</c> grace window. A compatibility
/// guard test freezes each payload's shape so a breaking change fails CI. <see cref="Version"/> is the
/// schema revision of the payload type (bumped on each additive/obsoleting change), not a routing
/// discriminator — every subscriber receives the same object and reads the fields it knows.
/// </para>
/// </summary>
public interface INotificationEvent
{
    /// <summary>
    /// The catalog wire name of this event (e.g. <c>alert:created</c>), matching a
    /// <see cref="Piro.Domain.Enums.NotificationEventType"/> value.
    /// </summary>
    string EventType { get; }

    /// <summary>The schema revision of this payload type — see the type-level remarks.</summary>
    int Version { get; }
}
