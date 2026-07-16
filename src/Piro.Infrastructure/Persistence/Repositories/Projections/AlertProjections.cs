using System.Linq.Expressions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared EF-translatable Alert → row projections, so <see cref="AlertRepository"/>'s query methods don't duplicate the null-safe field mapping.
/// </summary>
internal static class AlertProjections
{
    public static readonly Expression<Func<Alert, AlertSummaryRow>> ToSummaryRow = a => new AlertSummaryRow(
        a.Id,
        a.Check != null ? a.Check.Slug : null,
        a.Check != null ? a.Check.Name : null,
        a.Service != null ? a.Service.Slug : null,
        a.Service != null ? a.Service.Name : null,
        a.AlertConfig != null ? a.AlertConfig.Description : null,
        a.Message,
        a.ImpactAtFireTime,
        a.FiredAt,
        a.ResolvedAt,
        a.OccurrenceCount,
        a.IncidentId,
        a.EscalationPolicyId != null,
        a.Source);

    public static readonly Expression<Func<Alert, AlertDetailRow>> ToDetailRow = a => new AlertDetailRow(
        a.Id,
        a.Check != null ? a.Check.Slug : null,
        a.Check != null ? a.Check.Name : null,
        a.Service != null ? a.Service.Slug : null,
        a.Service != null ? a.Service.Name : null,
        a.AlertConfigId,
        a.AlertConfig != null ? a.AlertConfig.AlertFor : (Piro.Domain.Enums.AlertFor?)null,
        a.AlertConfig != null ? a.AlertConfig.AlertValue : null,
        a.AlertConfig != null ? a.AlertConfig.FailureThreshold : (int?)null,
        a.AlertConfig != null ? a.AlertConfig.SuccessThreshold : (int?)null,
        a.AlertConfig != null ? a.AlertConfig.Description : null,
        a.Message,
        a.ImpactAtFireTime,
        a.AlertConfig != null ? a.AlertConfig.Severity : (Piro.Domain.Enums.AlertSeverity?)null,
        a.FiredAt,
        a.ResolvedAt,
        a.OccurrenceCount,
        a.IncidentId,
        a.Incident != null ? a.Incident.Title : null,
        a.EscalationCurrentStep,
        a.AcknowledgedAt,
        a.AcknowledgedBy,
        a.Source,
        a.SourceRequestLog != null ? a.SourceRequestLog.RawPayload : null,
        a.SourceUrl);
}
