using Piro.Application.Models;

namespace Piro.Application.Interfaces;

/// <summary>
/// Sends an alert lifecycle event (trigger / resolve) to a shared, team-wide incident-management
/// channel — as opposed to <see cref="IPersonalNotificationDispatcher{TContent}"/>, which targets one individual's
/// personal handle. The target platform (PagerDuty, later Opsgenie/etc.) owns escalation and paging
/// once it receives a trigger; Piro only opens and closes the event (RFC 0004 §3).
/// <para>
/// The caller supplies the routing credentials (a PagerDuty routing key, resolved from the
/// Service↔Integration mapping) and a stable <c>dedupKey</c> that pairs a trigger with its later
/// resolve. This dispatcher neither reads the mapping nor decides where the dedup key is persisted —
/// that is the caller's concern (RFC 0004 §4.6), keeping the dispatcher pure and testable.
/// </para>
/// </summary>
public interface ISystemEventDispatcher
{

    /// <summary>Open string discriminator (RFC 0016 §4.4), defaulted from <see cref="Type"/> during the transition; dispatch moves to it in 5b.</summary>
    string IntegrationId { get; }

    /// <summary>
    /// Opens an event on the shared channel for the given alert. <paramref name="dedupKey"/> must be
    /// stable per logical alert so the later <see cref="ResolveAsync"/> references the same event.
    /// Returns true on success; false (logged) on a clean failure — never throws for a bad routing key.
    /// </summary>
    Task<bool> TriggerAsync(string routingKey, string dedupKey, AlertNotificationContext context, CancellationToken ct = default);

    /// <summary>Closes the event previously opened under <paramref name="dedupKey"/>.</summary>
    Task<bool> ResolveAsync(string routingKey, string dedupKey, CancellationToken ct = default);
}
