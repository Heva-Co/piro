using Microsoft.Extensions.Logging;
using Piro.Contracts;
using Piro.Integrations.Abstractions;
using Piro.Integrations.MobilePush.Crypto;
using Piro.Integrations.MobilePush.Transport;

namespace Piro.Integrations.MobilePush;

/// <summary>
/// Delivers a notification <see cref="Event"/> to every mobile device a user has registered (RFC 0016).
/// This is pure orchestration: it resolves the target user, reads their device tokens through the host
/// (<see cref="IDeviceTokenReader"/>), renders one neutral <see cref="PushMessage"/>, and fans it out to
/// all devices at once — each device delivered by the <see cref="IPushTransport"/> matching its platform.
/// The actual FCM/APNs wire work lives behind those transports, so this class never touches an HTTP
/// client or an SDK and reaches Piro only through <see cref="IIntegrationHost"/>.
/// <para>
/// It succeeds if at least one device accepted. Tokens the provider reports as unregistered are pruned
/// so a dead device stops being paged.
/// </para>
/// </summary>
public sealed class MobilePushNotificationDispatcher(
    IEnumerable<IPushTransport> transports,
    IPushPayloadSealer sealer,
    ILogger<MobilePushNotificationDispatcher> logger) : IIntegrationEventHandler
{
    public string IntegrationId => "MobilePush";

    public async Task<bool> HandleAsync(Event evt, EventDeliveryContext ctx, IIntegrationHost host, CancellationToken ct = default)
    {
        // Personal delivery: the engine puts the recipient user's id in ctx.Target (see the MobilePush
        // preference, whose Handle is the user id). A missing/invalid target is a config error, not a send.
        int.TryParse(ctx.Target, out var userId);
        // if (ctx.Mode != EventDeliveryMode.Personal || !int.TryParse(ctx.Target, out var userId))
        //     return false;

        var config = ctx.IntegrationInstanceId is { } instanceId
            ? await host.GetConfigAsync<MobilePushConfig>(instanceId, ct)
            : null;
        config ??= new MobilePushConfig();

        var reader = host.GetRequiredService<IDeviceTokenReader>();
        var devices = await reader.GetByUserIdAsync(userId, ct);
        if (devices.Count == 0)
        {
            logger.LogInformation("MobilePush: user {UserId} has no registered devices for {EventKey}.", userId, evt.EventKey);
            return false;
        }

        var message = Render(evt);

        var anyDelivered = false;
        var deadTokens = new List<string>();
        foreach (var device in devices)
        {
            // Platform alone no longer identifies a transport: Direct and Relay both serve Android.
            // Matching on the configured mode as well is what keeps the relay from shadowing FCM (or
            // vice versa) by DI registration order.
            var transport = transports.FirstOrDefault(
                t => t.Platform == device.Platform && t.Mode == config.Mode);
            if (transport is null)
            {
                logger.LogWarning(
                    "MobilePush: no {Mode} transport for platform {Platform}; device skipped for {EventKey}.",
                    config.Mode, device.Platform, evt.EventKey);
                continue;
            }

            // Sealed per device, because the envelope is bound to that device's public key. A device
            // registered before it published one gets the legacy cleartext path in Direct mode, and is
            // refused by the relay transport, which requires a ciphertext.
            var deviceMessage = message with { SealedPayload = TrySeal(message, device, evt) };

            var result = await transport.SendAsync(device.Token, deviceMessage, config, ct);
            switch (result)
            {
                case PushSendResult.Sent:
                    anyDelivered = true;
                    break;
                case PushSendResult.Unregistered:
                    deadTokens.Add(device.Token);
                    break;
                case PushSendResult.TransientFailure:
                case PushSendResult.NotConfigured:
                    break;
            }
        }

        if (deadTokens.Count > 0)
            await reader.PruneTokensAsync(deadTokens, ct);

        logger.LogInformation(
            "MobilePush fanned out {EventKey} to user {UserId}: {DeviceCount} device(s), delivered={Delivered}, pruned={Pruned}.",
            evt.EventKey, userId, devices.Count, anyDelivered, deadTokens.Count);

        return anyDelivered;
    }

    /// <summary>
    /// Seals the rendered message for one device, or returns null when it cannot be sealed. A null is not
    /// an error: a device registered before push encryption shipped has no public key and re-registers
    /// with one on its next app launch. A malformed key is logged and treated the same way, so one bad
    /// row never aborts the fan-out to a user's other devices.
    /// </summary>
    private string? TrySeal(PushMessage message, DeviceTokenInfo device, Event evt)
    {
        if (string.IsNullOrWhiteSpace(device.PushPublicKey))
            return null;

        var plaintext = new PushPlaintext
        {
            Title = message.Title,
            Body = message.Body,
            EventKey = message.EventKey,
            AlertId = message.AlertId,
            Url = message.Url,
        };

        try
        {
            return sealer.Seal(plaintext, device.PushPublicKey);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex,
                "MobilePush: could not seal for a {Platform} device (bad push public key); sending unsealed or skipping. Event {EventKey}.",
                device.Platform, evt.EventKey);
            return null;
        }
    }

    private static PushMessage Render(Event evt)
    {
        var isRecovery = evt is AlertResolvedEvent or IncidentResolvedEvent;
        var critical = !isRecovery && evt.Severity == EventSeverity.Critical;

        var title = isRecovery
            ? $"Recovered — {evt.Title}"
            : $"{evt.Severity.ToString().ToUpperInvariant()} — {evt.Title}";

        return new PushMessage
        {
            Title = title,
            Body = RenderBody(evt, isRecovery),
            Critical = critical,
            Url = evt.Url,
            EventKey = evt.EventKey,
            AlertId = (evt as AlertEvent)?.AlertId ?? 0,
        };
    }

    private static string RenderBody(Event evt, bool isRecovery)
    {
        if (evt is AlertEvent alert)
        {
            var status = alert.CurrentStatus ?? string.Empty;
            if (isRecovery)
                return $"{alert.CheckName} on {alert.ServiceName} has recovered.";
            var body = $"{alert.CheckName} on {alert.ServiceName} is {status}.".Trim();
            return string.IsNullOrWhiteSpace(alert.Description) ? body : $"{body} {alert.Description}";
        }
        return evt.Title;
    }
}
