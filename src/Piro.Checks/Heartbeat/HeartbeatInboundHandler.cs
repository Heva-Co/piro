using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// Handles a Heartbeat ping (RFC 0013): validate the token, record an UP data point, done. The target
/// calls <c>GET/POST /api/v1/checks/{checkId}/inbound?token=hb_…</c> (or the <c>X-Heartbeat-Token</c>
/// header). A pure (context) → outcome unit reaching Piro only through the allow-listed host, so it obeys
/// "checks know nothing" and is unit-testable without HTTP.
/// </summary>
public sealed class HeartbeatInboundHandler : ICheckInboundHandler
{
    // A fixed, no-parameter ping endpoint: nothing after the check id.
    public string InboundPathTemplate => "";

    public async Task<CheckInboundOutcome> HandleAsync(CheckInboundContext ctx, ICheckHost host, CancellationToken ct = default)
    {
        var token = ReadToken(ctx);
        if (string.IsNullOrEmpty(token))
            return CheckInboundOutcome.AuthFailed;

        var validator = host.GetRequiredService<ICheckTokenValidator>();
        if (!await validator.ValidateAsync(token, ct))
            return CheckInboundOutcome.AuthFailed;

        await host.GetRequiredService<ICheckPingRecorder>().RecordPingAsync(ct);
        return CheckInboundOutcome.Accepted;
    }

    private static string? ReadToken(CheckInboundContext ctx)
    {
        if (ctx.Query.TryGetValue("token", out var q) && !string.IsNullOrEmpty(q))
            return q;
        if (ctx.Headers.TryGetValue("X-Heartbeat-Token", out var h) && !string.IsNullOrEmpty(h))
            return h;
        return null;
    }
}
