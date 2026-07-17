namespace Piro.Domain.Enums;

/// <summary>
/// What happened to a single inbound webhook request — see <see cref="Entities.WebhookRequestLog"/>.
/// Lets an admin answer "how many of the last N posts to this webhook actually resulted in an
/// Alert" at a glance, per RFC 0001 §4.4.
/// </summary>
public enum WebhookRequestOutcome
{
    /// <summary>Produced or updated an Alert, anchored to a Check/Service via the source's correlation field.</summary>
    Accepted,

    /// <summary>
    /// Produced or updated an orphan Alert (no Check/Service) because no correlation field was
    /// configured on this source — an intentional, expected case, not a config error.
    /// </summary>
    AcceptedOrphan,

    /// <summary>
    /// Produced or updated an orphan Alert because the source's correlation field (e.g. GCP's
    /// <c>policy_name</c>) was present but didn't match any existing Check — likely a config
    /// mistake (typo, renamed Check) rather than a deliberate choice not to anchor. See RFC 0001 §8.
    /// </summary>
    CorrelationMismatch,

    /// <summary>Rejected — authentication failed (bad/missing token).</summary>
    AuthFailed,

    /// <summary>The request body didn't match the expected payload shape for this source.</summary>
    ParseError,

    /// <summary>A resend of an already-processed event (same fingerprint) — folded into the existing Alert's OccurrenceCount, no new one created.</summary>
    Deduplicated,
}
