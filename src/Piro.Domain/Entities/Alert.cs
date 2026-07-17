using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// Persistent instance of an <see cref="AlertConfig"/> firing. Exists independently of any
/// <see cref="Incident"/> — it is hooked to one only once it crosses an incident threshold
/// (see <see cref="AlertConfig.IncidentThresholdOccurrences"/> or concurrent-alert count).
/// </summary>
public class Alert
{
    public int Id { get; set; }

    /// <summary>
    /// Null for a webhook-sourced alert (RFC 0001) — there is no internal AlertConfig behind a
    /// third-party occurrence. Non-null (and dedup'd via <see cref="MessageFingerprint"/>) for
    /// every <see cref="AlertSource.Internal"/> alert.
    /// </summary>
    public int? AlertConfigId { get; set; }

    /// <summary>
    /// Null for an orphan alert (RFC 0001) — a third-party alert with no monitored resource to
    /// correlate against (e.g. a GCP alert policy with no matching <see cref="Domain.Entities.Check"/>).
    /// </summary>
    public int? CheckId { get; set; }

    /// <summary>Null for an orphan alert — see <see cref="CheckId"/>.</summary>
    public int? ServiceId { get; set; }

    /// <summary>Null until this Alert is hooked to an Incident (by occurrence or concurrent-count threshold).</summary>
    public int? IncidentId { get; set; }

    /// <summary>Impact captured at fire time from <see cref="AlertConfig.Severity"/> — frozen, not re-derived later.</summary>
    public ServiceStatus ImpactAtFireTime { get; set; }

    /// <summary>Frozen error text/payload at the moment of firing (e.g. "HTTP status code 503").</summary>
    public string? Message { get; set; }

    /// <summary>Deterministic normalization of <see cref="Message"/> (trim + lowercase + collapse whitespace) —
    /// exact-match dedup key, not fuzzy similarity.</summary>
    public string MessageFingerprint { get; set; } = string.Empty;

    public DateTimeOffset FiredAt { get; set; }

    /// <summary>Null while active. Set when the underlying <see cref="AlertConfig"/> recovers.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>How many consecutive failing evaluations with the same <see cref="MessageFingerprint"/>
    /// have been folded into this row since <see cref="FiredAt"/>.</summary>
    public int OccurrenceCount { get; set; } = 1;

    // ── On-call escalation (see EscalationCheckerService) ──────────────────────

    /// <summary>0-based index of the current escalation step. Null = not yet initialized.</summary>
    public int? EscalationCurrentStep { get; set; }

    /// <summary>When the current step became active. Step 0 uses <see cref="FiredAt"/>; subsequent steps use dispatch time.
    /// Also serves as the timestamp of the current step's last attempt, gating <see cref="EscalationStep.RetryIntervalMinutes"/>.</summary>
    public DateTimeOffset? EscalationStepStartedAt { get; set; }

    /// <summary>How many times the CURRENT escalation step has notified its on-call users. Reset to
    /// 0 each time escalation advances to a new step. Compared against <see cref="EscalationStep.MaxRetries"/>
    /// to decide when to hand off to the next step.</summary>
    public int EscalationStepAttempts { get; set; }

    /// <summary>Set when escalation stops because the LAST step exhausted its retries without an ACK
    /// or resolution. While non-null the alert is skipped by the escalation job — a persisted
    /// terminal escalation state, distinct from <see cref="ResolvedAt"/> (the problem is still open).
    /// Cleared if the alert is later acknowledged, so a human taking over can still drive escalation.</summary>
    public DateTimeOffset? EscalationExhaustedAt { get; set; }

    /// <summary>Unix timestamp (seconds) when a team member acknowledged this alert. Null if not yet acknowledged.</summary>
    public long? AcknowledgedAt { get; set; }

    /// <summary>Display name of the user who acknowledged the alert.</summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>Updated when a human acknowledges or otherwise interacts with the alert. Used for inactivity re-escalation.</summary>
    public DateTimeOffset? LastUserActivityAt { get; set; }

    /// <summary>Per-attempt delivery history for this alert's on-call escalation — see <see cref="EscalationDeliveryLog"/>.</summary>
    public ICollection<EscalationDeliveryLog> EscalationDeliveryLogs { get; set; } = [];

    /// <summary>
    /// Snapshotted once at creation from <see cref="Service.EscalationPolicyId"/> (anchored alerts)
    /// or <see cref="Integration.EscalationPolicyId"/> (orphan alerts) — never re-resolved live, so
    /// an admin editing the source policy mid-escalation doesn't change the behavior of an alert
    /// already partway through its steps. See RFC 0001 §4.6.
    /// </summary>
    public int? EscalationPolicyId { get; set; }

    /// <summary>Where this alert came from — see <see cref="AlertSource"/>.</summary>
    public AlertSource Source { get; set; } = AlertSource.Internal;

    /// <summary>
    /// The inbound webhook request that created this alert, if any (null for <see cref="AlertSource.Internal"/>).
    /// Points at the exact raw payload instead of duplicating it onto this row — see <see cref="WebhookRequestLog"/>.
    /// </summary>
    public int? SourceRequestLogId { get; set; }

    /// <summary>
    /// The source system's own identifier for this occurrence (e.g. GCP Cloud Monitoring's
    /// <c>incident.incident_id</c>) — null for <see cref="AlertSource.Internal"/> alerts, which
    /// dedup via <see cref="MessageFingerprint"/> instead. Dedup key is <c>(Source, ExternalId)</c>,
    /// not GCP-specific — any future webhook source (Alertmanager, etc.) reuses the same field.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Deep link into the source system's own console for this occurrence (e.g. GCP Cloud
    /// Monitoring's incident URL) — null for <see cref="AlertSource.Internal"/>, or for a source
    /// that doesn't provide one. Generic across sources, not GCP-specific.
    /// </summary>
    public string? SourceUrl { get; set; }

    public AlertConfig? AlertConfig { get; set; }
    public Check? Check { get; set; }
    public Service? Service { get; set; }
    public Incident? Incident { get; set; }
    public EscalationPolicy? EscalationPolicy { get; set; }
    public WebhookRequestLog? SourceRequestLog { get; set; }
}
