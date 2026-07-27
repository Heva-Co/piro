using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush.Transport;

/// <summary>
/// Sends one already-rendered push message to a single device token. Implementations wrap a concrete
/// provider (FCM, APNs) or no-op in development. Kept behind this interface so the dispatcher stays
/// pure orchestration — fan-out, severity mapping, failure handling — and never touches an HTTP client
/// or a provider SDK directly. The dispatcher selects the implementation per device by platform.
/// </summary>
public interface IPushTransport
{
    /// <summary>The platform this transport delivers to.</summary>
    DevicePushPlatform Platform { get; }

    /// <summary>
    /// Which delivery mode this transport implements. Selection is by (<see cref="Platform"/>,
    /// <see cref="Mode"/>) together: platform alone is no longer unique now that a relay transport
    /// exists for the same platform as the direct one.
    /// </summary>
    PushTransportMode Mode { get; }

    /// <summary>Whether this transport has the credentials it needs to send (from the resolved config).</summary>
    bool IsConfigured(MobilePushConfig config);

    /// <summary>Delivers one message to one device token.</summary>
    Task<PushSendResult> SendAsync(string token, PushMessage message, MobilePushConfig config, CancellationToken ct = default);
}

/// <summary>
/// How a push reaches the device. Persisted inside the integration's config JSON, so the numeric values
/// are part of that stored contract and must not be reordered.
/// </summary>
public enum PushTransportMode
{
    /// <summary>Straight to FCM/APNs with the operator's own credentials, for a self-built app.</summary>
    Direct = 0,

    /// <summary>
    /// Through the Heva push relay, which holds the provider credentials the store-published app was
    /// signed against. The relay is a blind forwarder and never sees the payload in the clear.
    /// </summary>
    Relay = 1,
}

/// <summary>A rendered push, neutral to the transport. The transport maps it to FCM/APNs wire format.</summary>
public sealed record PushMessage
{
    public required string Title { get; init; }
    public required string Body { get; init; }

    /// <summary>When true, the push must wake the device and bypass Do Not Disturb (a critical page).</summary>
    public required bool Critical { get; init; }

    /// <summary>Deep-link URL to the alert/incident, carried as data so the app can open the right screen.</summary>
    public string? Url { get; init; }

    /// <summary>Catalog event wire name (e.g. "alert:created"), carried as data for the app.</summary>
    public required string EventKey { get; init; }

    /// <summary>The alert id, carried as data so the app can open the alert detail and acknowledge it. 0 when not an alert event.</summary>
    public int AlertId { get; init; }

    /// <summary>
    /// The sealed envelope for this specific device, when the dispatcher could seal for it. Every field
    /// above is already inside it; a transport that has this must put it on the wire and send nothing
    /// else, so the content never leaves Piro in the clear.
    /// </summary>
    public string? SealedPayload { get; init; }
}

/// <summary>Outcome of a single-token send, so the dispatcher knows whether to prune or retry.</summary>
public enum PushSendResult
{
    /// <summary>Delivered (or accepted by the provider).</summary>
    Sent,

    /// <summary>The provider reported the token as permanently invalid — prune it.</summary>
    Unregistered,

    /// <summary>A transient failure — keep the token, count the failure.</summary>
    TransientFailure,

    /// <summary>This platform has no credentials configured — nothing was attempted.</summary>
    NotConfigured,
}
