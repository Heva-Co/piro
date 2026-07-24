using Piro.Contracts;

namespace Piro.Integrations.Abstractions;

/// <summary>
/// The single entry point through which an integration reacts to a notification <see cref="Event"/>
/// (RFC 0016). One contract for every delivery: the engine matches the event against subscriptions,
/// resolves where it goes, and hands the integration the event plus an <see cref="EventDeliveryContext"/>.
/// The integration decides what to do — format and send to a personal handle, post to a team channel,
/// or ignore it. It reaches the rest of Piro only through <see cref="IIntegrationHost"/> ("integrations
/// know nothing", §4.2b).
/// <para>
/// This replaces the earlier split of personal/channel/system-event dispatchers with one handler. The
/// engine owns routing (it knows the subscription, the recipient, the channel target); the integration
/// owns behavior (how the event is rendered and delivered on its medium).
/// </para>
/// </summary>
public interface IIntegrationEventHandler
{
    /// <summary>The integration this handler belongs to (its <see cref="IIntegration.IntegrationId"/>).</summary>
    string IntegrationId { get; }

    /// <summary>
    /// Handle one matched event. <paramref name="ctx"/> carries the resolved target and mode; the
    /// integration formats and delivers, reaching services (HttpClient, its own config) via
    /// <paramref name="host"/>. Returns true on success; false (logged) on a clean, recoverable failure.
    /// </summary>
    Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default);
}

/// <summary>Everything the engine resolved about where and how this event should be delivered.</summary>
public sealed record EventDeliveryContext
{
    /// <summary>
    /// The configured integration instance (a <c>Guid</c>) this delivery uses, so the handler can ask
    /// the host for that instance's config/token. Null for a personal channel that needs no integration
    /// instance (e.g. email to the user's own address).
    /// </summary>
    public Guid? IntegrationInstanceId { get; init; }

    /// <summary>
    /// Where to deliver, already resolved by the engine: a person's handle for
    /// <see cref="EventDeliveryMode.Personal"/>, or the channel target for
    /// <see cref="EventDeliveryMode.Channel"/> (null when the integration's config already identifies
    /// the destination, e.g. a webhook URL).
    /// </summary>
    public string? Target { get; init; }

    /// <summary>Whether this goes to one person or a shared team channel.</summary>
    public EventDeliveryMode Mode { get; init; }
}

public enum EventDeliveryMode
{
    Personal,
    Channel,
}
