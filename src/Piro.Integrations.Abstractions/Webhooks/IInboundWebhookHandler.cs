namespace Piro.Integrations.Abstractions;

/// <summary>
/// What Piro should report back to the caller for one inbound webhook request (RFC 0016). The handler
/// decides this after validating/parsing; Piro maps it to an HTTP status and logs it. Deliberately
/// coarse — a webhook source (e.g. GCP) retries on non-2xx, so only genuinely rejectable requests
/// (bad auth, unparseable body) should map to a failure.
/// </summary>
public enum WebhookOutcome
{
    /// <summary>Processed — an alert was created/updated/resolved (or intentionally nothing, e.g. a dedup).</summary>
    Accepted,

    /// <summary>Rejected — authentication failed (bad/missing token).</summary>
    AuthFailed,

    /// <summary>The request body didn't match the shape this handler expects.</summary>
    ParseError,
}

/// <summary>
/// Everything a handler gets for one inbound request (RFC 0016). Piro builds it from the HTTP request
/// after resolving the route <c>api/v1/webhooks/{integrationId}/{**rest}</c>: the integration instance
/// the URL targets, the raw body, the query and header dictionaries (a webhook source like GCP carries
/// its token in the query string, since it can't send custom headers), and the values captured from the
/// handler's own <see cref="IInboundWebhookHandler.WebhookPathTemplate"/> applied to <c>{rest}</c>.
/// <para>
/// It is a plain POCO — no ASP.NET types — so the handler stays free of the web framework and is a pure
/// (context) → outcome unit that can be unit-tested without HTTP (RFC 0016 "integrations know nothing").
/// </para>
/// </summary>
public sealed record InboundWebhookContext(
    Guid IntegrationId,
    string RawPayload,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> RouteValues);

/// <summary>
/// Handles inbound webhook requests for one integration (RFC 0016) — the replacement for a bespoke
/// per-source ingestion service living in Piro's core. The handler owns everything source-specific:
/// validating the token, parsing the payload, deciding what alert to record/resolve. It writes into
/// Piro only through <see cref="IAlertService"/> (resolved from the host), never a repository.
/// <para>
/// Registered imperatively in <see cref="IIntegration.Configure"/> via <see cref="IWebhookHost"/>. The
/// integration must declare <see cref="IntegrationCapability.CreatesAlerts"/> (the same capability gates
/// resolving <see cref="IAlertService"/> from the host).
/// </para>
/// </summary>
public interface IInboundWebhookHandler
{
    /// <summary>The integration id this handler belongs to (e.g. "GcpCloudMonitoringWebhook").</summary>
    string IntegrationId { get; }

    /// <summary>
    /// The path template matched against the URL segment(s) after the integration-instance id, i.e. the
    /// <c>{rest}</c> in <c>api/v1/webhooks/{integrationId}/{rest}</c>. Empty string for a fixed
    /// no-parameter webhook; <c>"{region}"</c> or <c>"{region}/{env}"</c> to capture segments into
    /// <see cref="InboundWebhookContext.RouteValues"/>. Because the instance id already disambiguates
    /// which integration handles the request, templates never collide across integrations.
    /// </summary>
    string WebhookPathTemplate { get; }

    /// <summary>
    /// Validates and processes one request, using <paramref name="host"/> (for
    /// <see cref="IAlertService"/> and its own config) to record/resolve alerts. Returns the outcome for
    /// Piro to log and map to HTTP. Must not throw for a malformed/unauthenticated request.
    /// </summary>
    Task<WebhookOutcome> HandleAsync(InboundWebhookContext ctx, IIntegrationHost host, CancellationToken ct = default);
}
