using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Checks.Abstractions;
using Piro.Domain.Enums;
using Piro.Infrastructure.Auth;

namespace Piro.Infrastructure.Checks;

/// <summary>
/// Scoped holder for the check an inbound request targets (RFC 0013). The generic inbound controller
/// resolves the check by the URL id and sets this before dispatching to the check's handler, so the
/// handler's host-resolved services (<see cref="ICheckTokenValidator"/>, <see cref="ICheckPingRecorder"/>)
/// act on exactly that check without the handler ever naming an id.
/// </summary>
internal sealed class CurrentInboundCheck
{
    public int CheckId { get; set; }
}

/// <summary>Validates a check-inbound token against the current inbound check (RFC 0013), via <see cref="ApiKeyService"/>.</summary>
internal sealed class CheckInboundTokenValidator(CurrentInboundCheck current, ApiKeyService apiKeys) : ICheckTokenValidator
{
    public Task<bool> ValidateAsync(string rawToken, CancellationToken ct = default) =>
        apiKeys.ValidateCheckInboundAsync(rawToken, current.CheckId, ct);
}

/// <summary>
/// Records an UP data point for the current inbound check (RFC 0013) through the shared
/// <see cref="ICheckResultIngester"/>, so the check flips to UP immediately and recovery-side alert
/// thresholds evaluate — the same path a scheduled run uses.
/// </summary>
internal sealed class CheckInboundPingRecorder(CurrentInboundCheck current, ICheckResultIngester ingester) : ICheckPingRecorder
{
    public Task RecordPingAsync(CancellationToken ct = default) =>
        ingester.IngestAsync(current.CheckId, CheckExecutionResult.Of(ServiceStatus.UP), "default", ct);
}

/// <summary>
/// Resolves a check's inbound handler and runs it (RFC 0013). Binds <see cref="CurrentInboundCheck"/> so
/// the handler's host-resolved token validator / ping recorder act on this check, then dispatches through
/// the allow-listed host. Returns null when the check type ships no handler (a 404 for the controller).
/// </summary>
internal sealed class CheckInboundDispatcher(
    ICheckRegistry registry,
    ICheckHost host,
    CurrentInboundCheck current) : ICheckInboundDispatcher
{
    public async Task<CheckInboundOutcome?> DispatchAsync(int checkId, string checkType, CheckInboundContext ctx, CancellationToken ct = default)
    {
        var handler = registry.Find(checkType)?.ProvidedInboundHandler();
        if (handler is null)
            return null;

        // Reject a rest path that doesn't match the handler's template (a fixed "" template requires empty rest).
        if (!string.Equals(ctx.Rest ?? "", handler.InboundPathTemplate ?? "", StringComparison.Ordinal))
            return null;

        current.CheckId = checkId;
        return await handler.HandleAsync(ctx, host, ct);
    }
}
