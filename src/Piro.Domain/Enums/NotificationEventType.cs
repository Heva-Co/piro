using Piro.Domain.Attributes;

namespace Piro.Domain.Enums;

/// <summary>
/// The closed, code-owned catalog of events Piro emits (RFC 0009 §4.2). An admin subscribes to these;
/// they are never defined at runtime. Each value carries a <see cref="NotificationEventAttribute"/>
/// giving its stable wire name (<c>domain:...:verb</c>) and a description of when it fires — the
/// single source of truth the subscription UI and payload discovery read by reflection. Adding an
/// event is a code change; wire names are permanent once shipped.
/// </summary>
public enum NotificationEventType
{
    /// <summary>An Alert row was created for a monitored service (or ingested from a third party).</summary>
    [NotificationEvent(NotificationEventNames.AlertCreated,
        "An alert was opened for a monitored service — a check crossed its failure threshold, or a third-party alert was ingested.")]
    AlertCreated,

    /// <summary>A human acknowledged an active alert.</summary>
    [NotificationEvent(NotificationEventNames.AlertAcknowledged,
        "A team member acknowledged an active alert, taking ownership of the response.")]
    AlertAcknowledged,

    /// <summary>An active alert cleared (recovered), whether automatically or by resolution.</summary>
    [NotificationEvent(NotificationEventNames.AlertResolved,
        "An active alert cleared — the underlying service recovered or the alert was resolved.")]
    AlertResolved,

    /// <summary>An incident was opened (born in the Investigating state).</summary>
    [NotificationEvent(NotificationEventNames.IncidentCreated,
        "An incident was opened, entering the Investigating state.")]
    IncidentCreated,

    /// <summary>An incident reached a final state (Resolved or Merged).</summary>
    [NotificationEvent(NotificationEventNames.IncidentResolved,
        "An incident reached a final state — resolved or merged into another incident.")]
    IncidentResolved,

    /// <summary>
    /// An integration's connection expired or was disconnected and needs operator attention.
    /// NOTE: defined in the catalog but not yet emitted — nothing detects integration expiry today.
    /// The emitter (an OAuth-token check and/or the disconnect flow) is future work; until then a
    /// subscription to this event is valid but never fires.
    /// </summary>
    [NotificationEvent(NotificationEventNames.SystemIntegrationExpired,
        "An integration's connection expired or was disconnected — it can no longer be used until reconnected.")]
    SystemIntegrationExpired,
}
