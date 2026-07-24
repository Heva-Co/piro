using Piro.Checks.Abstractions;

namespace Piro.Application.Interfaces;

/// <summary>
/// Dispatches one inbound check request to the handler its check type ships (RFC 0013). The generic
/// inbound controller calls this with the resolved check id and the request context; the implementation
/// binds the current-inbound-check scope, looks up the check's <see cref="ICheckInboundHandler"/>, and
/// runs it through the allow-listed host — so the controller stays free of the check SDK's internals and
/// the "checks know nothing" boundary is enforced in one place.
/// </summary>
public interface ICheckInboundDispatcher
{
    /// <summary>
    /// Runs the inbound handler for the check with <paramref name="checkId"/> (type resolved internally).
    /// Returns null when the check's type ships no inbound handler (a 404 at the controller); otherwise
    /// the handler's outcome.
    /// </summary>
    Task<CheckInboundOutcome?> DispatchAsync(int checkId, string checkType, CheckInboundContext ctx, CancellationToken ct = default);
}
