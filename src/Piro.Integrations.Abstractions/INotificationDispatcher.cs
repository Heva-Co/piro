using Piro.Contracts;

namespace Piro.Integrations.Abstractions;

/// <summary>
/// How an integration delivers a notification <see cref="Event"/> (RFC 0016). Replaces the core's
/// old <c>IPersonalNotificationDispatcher</c>/<c>IChannelNotificationDispatcher</c> pairing with a
/// single contract that speaks the neutral <see cref="Event"/> hierarchy and reaches Piro only
/// through <see cref="IIntegrationHost"/> — never a domain entity, repository, or ambient service
/// ("integrations know nothing", §4.2b).
/// <para>
/// The delivery <em>mode</em> (to one person's handle vs. a shared channel) is carried by
/// <see cref="NotificationDelivery.Mode"/>, not by separate interfaces: a dispatcher inspects it, or
/// declares which modes it supports through its manifest capabilities.
/// </para>
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>The integration this dispatcher belongs to (its <see cref="IIntegration.IntegrationId"/>).</summary>
    string IntegrationId { get; }

    /// <summary>
    /// Deliver <paramref name="evt"/> for <paramref name="delivery"/>. The host provides the services
    /// the dispatcher is allowed to use (HttpClient, secret protector, this integration's config).
    /// Returns true on success; false (logged) on a clean, recoverable failure.
    /// </summary>
    Task<bool> SendAsync(Event evt, NotificationDelivery delivery, IIntegrationHost host, CancellationToken ct = default);
}

/// <summary>Where and how a single notification should be delivered — the routing target, resolved by the core.</summary>
public sealed record NotificationDelivery
{
    /// <summary>Personal handle (chat id, phone, email) or channel target (webhook URL, room) — how depends on <see cref="Mode"/>.</summary>
    public required string Target { get; init; }

    /// <summary>Whether this goes to one person (<see cref="NotificationMode.Personal"/>) or a shared channel (<see cref="NotificationMode.Channel"/>).</summary>
    public required NotificationMode Mode { get; init; }

    /// <summary>The configured integration's id, so the dispatcher can ask the host for its config/token. Null for integration-less personal channels (e.g. a raw handle).</summary>
    public Guid? IntegrationId { get; init; }
}

public enum NotificationMode
{
    Personal,
    Channel,
}
