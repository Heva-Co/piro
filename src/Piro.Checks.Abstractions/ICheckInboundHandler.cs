namespace Piro.Checks.Abstractions;

/// <summary>
/// What Piro reports back to the caller of one inbound check request (RFC 0013), mirroring the
/// integration webhook outcome. Deliberately coarse: only genuinely rejectable requests are non-2xx.
/// </summary>
public enum CheckInboundOutcome
{
    /// <summary>Processed — the ping was recorded (or intentionally ignored).</summary>
    Accepted,

    /// <summary>Rejected — the token was missing or invalid.</summary>
    AuthFailed,

    /// <summary>The request didn't match what this handler expects (bad body / shape).</summary>
    BadRequest,
}

/// <summary>
/// Everything a check inbound handler gets for one request (RFC 0013). A plain POCO — no ASP.NET types —
/// so the handler stays a pure (context) → outcome unit, testable without HTTP and free of the web
/// framework, honoring "checks know nothing about Piro". Piro builds it from the HTTP request after
/// resolving the route <c>api/v1/checks/{checkId}/inbound/{**rest}</c>.
/// </summary>
public sealed record CheckInboundContext(
    /// <summary>The path segment(s) after the check id — the <c>{rest}</c>, matched to the handler's template.</summary>
    string Rest,
    /// <summary>The raw request body (empty for a GET ping).</summary>
    string RawBody,
    /// <summary>Query-string values (a cron/CI pinger carries its token here).</summary>
    IReadOnlyDictionary<string, string> Query,
    /// <summary>Request headers.</summary>
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Handles inbound requests for one check type (RFC 0013 — the Heartbeat ping). The check ships this via
/// <see cref="ICheck.ProvidedInboundHandler"/>; a single generic controller resolves the check by id and
/// dispatches to it (404 when the check's type ships no handler), so there is no per-check controller in
/// core, exactly like integration webhooks. The handler validates its own token and records a data point
/// through the allow-listed <see cref="ICheckHost"/> — never a repository — so the "checks know nothing"
/// boundary holds. It must not throw for a malformed/unauthenticated request; it returns the outcome.
/// </summary>
public interface ICheckInboundHandler
{
    /// <summary>
    /// The path template matched against the URL after the check id (the <c>{rest}</c> in
    /// <c>api/v1/checks/{checkId}/inbound/{rest}</c>). Empty string for a fixed no-parameter endpoint.
    /// </summary>
    string InboundPathTemplate { get; }

    /// <summary>
    /// Validates and processes one inbound request, using <paramref name="host"/> to resolve what it
    /// needs (its token validator, the data-point writer) and returning the outcome for Piro to map to
    /// HTTP. Must not throw for a bad/unauthenticated request.
    /// </summary>
    Task<CheckInboundOutcome> HandleAsync(CheckInboundContext ctx, ICheckHost host, CancellationToken ct = default);
}
