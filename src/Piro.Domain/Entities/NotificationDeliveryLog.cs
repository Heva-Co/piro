using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// One row per delivery attempt of a notification event to a specific destination (RFC 0009 §4.6).
/// Serves two purposes at once: the <see cref="IdempotencyKey"/> UNIQUE constraint makes at-least-once
/// outbox delivery effectively-once (a duplicate is short-circuited to <see cref="DeliveryStatus.Skipped"/>),
/// and the row is the durable answer to an admin's "why didn't it reach Slack?". Retention/purge of
/// this table is out of scope for v1 — it grows unbounded (tracked as a follow-up).
/// </summary>
public class NotificationDeliveryLog
{
    public long Id { get; set; }

    /// <summary>
    /// Deterministic per (event × destination): <c>{eventType}:{entityId}:{subscriptionId}</c>.
    /// Identical on every retry, so a UNIQUE index turns a re-send into a no-op skip.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>The catalog wire name of the delivered event.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The subscription that routed this delivery. Nullable for engine-level deliveries with no subscription.</summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>Personal | Channel | Integration — which delivery contract carried it.</summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>
    /// The integration type behind the delivery (e.g. GoogleChat, Telegram, Email) — so the admin feed
    /// can show its icon. Channel and Integration deliveries always have one; a Personal delivery carries
    /// the user's channel type. Null when no integration was involved (e.g. a skip before resolution).
    /// </summary>
    public string? IntegrationType { get; set; }

    /// <summary>
    /// The specific integration instance behind a Channel/Integration delivery — so the feed can be
    /// filtered to one integration's activity. Null for Personal deliveries (no integration instance).
    /// </summary>
    public Guid? IntegrationId { get; set; }

    /// <summary>Human-readable destination for the admin (e.g. "Slack #ops", "email:jane@…").</summary>
    public string TargetDescriptor { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="DeliveryStatus.Failed"/>. Null otherwise.</summary>
    public string? Error { get; set; }

    public DateTime AttemptedAt { get; set; }
}
