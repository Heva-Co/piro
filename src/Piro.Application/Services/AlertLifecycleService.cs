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
    /// </summary>
    public async Task<Alert> RecordOccurrenceAsync(
        AlertConfig config, Check check, Service service, string? message, CancellationToken ct = default)
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
            CheckId = check.Id,
            ServiceId = service.Id,
            ImpactAtFireTime = config.Severity == AlertSeverity.Critical ? ServiceStatus.DOWN : ServiceStatus.DEGRADED,
            Message = message,
            MessageFingerprint = fingerprint,
            FiredAt = DateTimeOffset.UtcNow,
            OccurrenceCount = 1,
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

    /// <summary>Deterministic exact-match normalization: trim, lowercase, collapse whitespace. Not fuzzy similarity.</summary>
    private static string Fingerprint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        var collapsed = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Trim().ToLowerInvariant();
    }
}
