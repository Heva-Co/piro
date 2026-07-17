using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>
/// Manages the fired/resolved lifecycle of <see cref="Alert"/> rows for an <see cref="AlertConfig"/>.
/// Deduplicates by exact-match message fingerprint: a repeated failure with the same normalized
/// message accumulates onto the active Alert; a different message closes it and opens a new one.
/// This is the single point that would need to change if deduplication is later refined
/// (e.g. fuzzy similarity instead of exact match).
/// </summary>
public class AlertLifecycleService(IAlertRepository alertRepository)
{
    /// <summary>
    /// Records a failing occurrence for the given AlertConfig. Creates a new Alert if none is active,
    /// or if the active one has a different message fingerprint (distinct root cause); otherwise
    /// increments the active Alert's OccurrenceCount and refreshes its Message.
    /// <paramref name="check"/>/<paramref name="service"/> are null for an orphan alert (RFC 0001
    /// §4.2) — a third-party alert with no correlatable resource. <paramref name="escalationPolicyId"/>
    /// is snapshotted onto the created Alert (see RFC 0001 §4.6) — pass the resolved <see cref="Service"/>'s
    /// policy for anchored alerts, or the source <see cref="Integration"/>'s for orphan ones.
    /// </summary>
    public async Task<Alert> RecordOccurrenceAsync(
        AlertConfig config,
        Check? check,
        Service? service,
        string? message,
        CancellationToken ct = default,
        int? escalationPolicyId = null,
        AlertSource source = AlertSource.Internal,
        int? sourceRequestLogId = null)
    {
        var fingerprint = Fingerprint(message);
        var active = await alertRepository.GetActiveForConfigAsync(config.Id, ct);

        if (active is not null && active.MessageFingerprint == fingerprint)
        {
            active.OccurrenceCount++;
            active.Message = message;
            return await alertRepository.UpdateAsync(active, ct);
        }

        if (active is not null)
        {
            active.ResolvedAt = DateTimeOffset.UtcNow;
            await alertRepository.UpdateAsync(active, ct);
        }

        var created = new Alert
        {
            AlertConfigId = config.Id,
            CheckId = check?.Id,
            ServiceId = service?.Id,
            ImpactAtFireTime = config.Severity == AlertSeverity.Critical ? ServiceStatus.DOWN : ServiceStatus.DEGRADED,
            Message = message,
            MessageFingerprint = fingerprint,
            FiredAt = DateTimeOffset.UtcNow,
            OccurrenceCount = 1,
            EscalationPolicyId = escalationPolicyId,
            Source = source,
            SourceRequestLogId = sourceRequestLogId,
        };

        return await alertRepository.CreateAsync(created, ct);
    }

    /// <summary>Closes the active Alert for the given AlertConfig, if one exists.</summary>
    public async Task ResolveActiveAlertAsync(int alertConfigId, CancellationToken ct = default)
    {
        var active = await alertRepository.GetActiveForConfigAsync(alertConfigId, ct);
        if (active is null) return;

        active.ResolvedAt = DateTimeOffset.UtcNow;
        await alertRepository.UpdateAsync(active, ct);
    }

    /// <summary>
    /// Records a firing occurrence from a third-party webhook source (RFC 0001 §4.6/§4.8) — there is
    /// no <see cref="AlertConfig"/> behind this Alert, so it can't reuse <see cref="RecordOccurrenceAsync"/>'s
    /// AlertConfig-based dedup. Deduplicates instead on <c>(Source, ExternalId)</c> — the source
    /// system's own occurrence identifier (e.g. GCP Cloud Monitoring's <c>incident.incident_id</c>),
    /// which stays stable across a renotify/re-delivery of the same underlying occurrence.
    /// <paramref name="check"/>/<paramref name="service"/> are null for an orphan alert. Returns the
    /// existing Alert unchanged if one with this (source, externalId) already exists and is still active.
    /// </summary>
    public async Task<Alert> RecordExternalOccurrenceAsync(
        AlertSource source,
        string externalId,
        Check? check,
        Service? service,
        string? message,
        ServiceStatus impact,
        int? escalationPolicyId,
        int? sourceRequestLogId,
        string? sourceUrl = null,
        CancellationToken ct = default)
    {
        var existing = await alertRepository.GetByExternalIdAsync(source, externalId, ct);
        if (existing is not null && existing.ResolvedAt is null)
        {
            existing.OccurrenceCount++;
            existing.Message = message;
            return await alertRepository.UpdateAsync(existing, ct);
        }

        var created = new Alert
        {
            CheckId = check?.Id,
            ServiceId = service?.Id,
            ImpactAtFireTime = impact,
            Message = message,
            MessageFingerprint = Fingerprint(message),
            FiredAt = DateTimeOffset.UtcNow,
            OccurrenceCount = 1,
            EscalationPolicyId = escalationPolicyId,
            Source = source,
            SourceRequestLogId = sourceRequestLogId,
            ExternalId = externalId,
            SourceUrl = sourceUrl,
        };

        return await alertRepository.CreateAsync(created, ct);
    }

    /// <summary>
    /// Closes the Alert matching this webhook source's (Source, ExternalId), if one exists and is
    /// still active — the "incident.state: closed" half of RFC 0001 §4.6. No-op if no such Alert
    /// exists (a "closed" for an occurrence Piro never recorded, or already resolved).
    /// </summary>
    public async Task<Alert?> ResolveExternalOccurrenceAsync(AlertSource source, string externalId, CancellationToken ct = default)
    {
        var active = await alertRepository.GetByExternalIdAsync(source, externalId, ct);
        if (active is null) return null;
        if (active.ResolvedAt is not null) return active;

        active.ResolvedAt = DateTimeOffset.UtcNow;
        return await alertRepository.UpdateAsync(active, ct);
    }

    /// <summary>Deterministic exact-match normalization: trim, lowercase, collapse whitespace. Not fuzzy similarity.</summary>
    private static string Fingerprint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        var collapsed = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Trim().ToLowerInvariant();
    }
}
