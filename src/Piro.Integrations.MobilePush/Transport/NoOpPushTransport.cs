using Microsoft.Extensions.Logging;
using Piro.Integrations.Abstractions;

namespace Piro.Integrations.MobilePush.Transport;

/// <summary>
/// A stand-in transport for a platform with no credentials configured (e.g. local development without
/// a Firebase project or Apple key). It never delivers — it logs the intended push and reports
/// <see cref="PushSendResult.NotConfigured"/> — so the end-to-end fan-out can be exercised without real
/// FCM/APNs. Registered only when the concrete transport for a platform reports it isn't configured.
/// </summary>
public sealed class NoOpPushTransport(DevicePushPlatform platform, ILogger<NoOpPushTransport> logger) : IPushTransport
{
    public DevicePushPlatform Platform => platform;

    public bool IsConfigured(MobilePushConfig config) => false;

    public Task<PushSendResult> SendAsync(string token, PushMessage message, MobilePushConfig config, CancellationToken ct = default)
    {
        logger.LogInformation(
            "MobilePush {Platform} not configured — would send \"{Title}\" (critical={Critical}) to token {TokenPrefix}…",
            platform, message.Title, message.Critical, token.Length > 8 ? token[..8] : token);
        return Task.FromResult(PushSendResult.NotConfigured);
    }
}
