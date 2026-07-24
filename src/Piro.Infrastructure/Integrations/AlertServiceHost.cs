using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Enums;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// The concrete <see cref="IAlertService"/> (RFC 0016) — the bounded seam an inbound integration uses to
/// push alerts into Piro. It maps the neutral request onto Piro's <see cref="AlertLifecycleService"/>
/// (dedup, publish, escalation), resolving per-instance data (like the escalation policy) from the
/// integration row so the integration never has to know about it.
/// </summary>
internal sealed class AlertServiceHost(
    AlertLifecycleService alertLifecycle,
    IIntegrationRepository integrationRepo) : IAlertService
{
    public async Task<int> RecordOccurrenceAsync(
        Guid integrationId,
        string externalId,
        AlertImpact impact,
        string? message,
        string? sourceUrl = null,
        CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");

        var alert = await alertLifecycle.RecordExternalOccurrenceAsync(
            source: SourceFor(integration.Type),
            externalId: externalId,
            check: null,
            service: null,
            message: message,
            impact: impact == AlertImpact.Down ? ServiceStatus.DOWN : ServiceStatus.DEGRADED,
            escalationPolicyId: integration.EscalationPolicyId,
            sourceRequestLogId: null,
            sourceUrl: sourceUrl,
            ct: ct);

        return alert.Id;
    }

    public async Task<int?> ResolveOccurrenceAsync(Guid integrationId, string externalId, CancellationToken ct = default)
    {
        var integration = await integrationRepo.GetByIdAsync(integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found.");

        var resolved = await alertLifecycle.ResolveExternalOccurrenceAsync(SourceFor(integration.Type), externalId, ct);
        return resolved?.Id;
    }

    // The last place a concrete integration id maps to the closed AlertSource enum. AlertSource is still
    // a Domain enum (persisted on Alert.Source); giving it the open string-discriminator treatment like
    // IntegrationType is a follow-up that needs a data migration. Until then, map here.
    private static AlertSource SourceFor(string integrationId) => integrationId switch
    {
        "GcpCloudMonitoringWebhook" => AlertSource.GcpCloudMonitoring,
        _ => throw new InvalidOperationException(
            $"Integration '{integrationId}' has no mapped AlertSource — add it when a new inbound integration ships."),
    };
}
