namespace Piro.Checks.Abstractions;

/// <summary>
/// Validates an inbound check token (RFC 0013). Resolved by an inbound handler through the allow-listed
/// <see cref="ICheckHost"/>; the implementation checks the token against the check the current inbound
/// request is for (bound by the controller before dispatch), so the handler passes only the raw token and
/// never a check id. A heartbeat token is scoped and non-privileged — a leak pings one check, nothing more.
/// </summary>
public interface ICheckTokenValidator
{
    /// <summary>True if <paramref name="rawToken"/> is a valid, active token for the current inbound check.</summary>
    Task<bool> ValidateAsync(string rawToken, CancellationToken ct = default);
}

/// <summary>
/// Records an inbound "the target is alive" ping as an UP data point for the current inbound check
/// (RFC 0013), flowing through the same ingestion path a scheduled run uses — so the check flips to UP
/// immediately, fires its status-changed event, and evaluates recovery-side alert thresholds. Resolved by
/// an inbound handler through the allow-listed <see cref="ICheckHost"/>; the handler names no check id.
/// </summary>
public interface ICheckPingRecorder
{
    /// <summary>Records an UP data point at "now" for the current inbound check.</summary>
    Task RecordPingAsync(CancellationToken ct = default);
}
