namespace Piro.Integrations.Abstractions;

/// <summary>
/// The bounded seam through which an inbound integration pushes alerts into Piro (RFC 0016). An
/// integration that ingests third-party signals (a webhook) never touches Piro's alert repositories or
/// the <c>AlertLifecycleService</c> directly — it resolves this from the host and reports occurrences.
/// <para>
/// CAPABILITY-GATED: only an integration whose manifest declares
/// <see cref="IntegrationCapability.CreatesAlerts"/> may resolve this via
/// <c>host.GetRequiredService&lt;IAlertService&gt;()</c>; asking for it without the capability throws.
/// </para>
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Records an external alert occurrence for the given integration instance, creating a new alert or
    /// folding into the existing active one keyed by (this integration, <paramref name="externalId"/>).
    /// The severity is neutral (<see cref="AlertImpact"/>); Piro maps it to its own status. Returns the
    /// created/updated alert's id.
    /// </summary>
    Task<int> RecordOccurrenceAsync(
        Guid integrationId,
        string externalId,
        AlertImpact impact,
        string? message,
        string? sourceUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the active alert matching (this integration, <paramref name="externalId"/>), if any.
    /// No-op when there is no matching active alert. Returns the resolved alert's id, or null if none.
    /// </summary>
    Task<int?> ResolveOccurrenceAsync(
        Guid integrationId,
        string externalId,
        CancellationToken ct = default);
}

/// <summary>
/// Neutral severity an inbound integration assigns to an occurrence — Piro maps it to its internal
/// service status. Deliberately not Piro's <c>ServiceStatus</c>: an integration stays free of Piro's
/// domain enums (RFC 0016).
/// </summary>
public enum AlertImpact
{
    /// <summary>A degradation / warning-level signal.</summary>
    Degraded,

    /// <summary>A full outage / critical signal.</summary>
    Down,
}
