using Piro.Application.Models;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Application.Notifications;

/// <summary>
/// Maps the core's notification contexts (<see cref="AlertNotificationContext"/>,
/// <see cref="IncidentNotificationContext"/>) onto the neutral <see cref="Event"/> hierarchy an
/// integration dispatcher consumes (RFC 0016). This is the edge where the domain model is translated
/// into the contract, so integration assemblies never reference a Piro.Domain type. The concrete
/// event subtype is chosen from the catalog wire name that fired.
/// </summary>
public static class EventMapper
{
    /// <summary>Builds the neutral event for an alert context and the wire name that fired (alert:created/acknowledged/resolved).</summary>
    public static Event ToEvent(this AlertNotificationContext ctx, string wireName)
    {
        var common = new
        {
            Severity = ToEventSeverity(ctx.Severity),
            ctx.FiredAt,
            ctx.FiredAtDisplay,
            Title = ctx.Title(),
            Url = ctx.AlertUrl,
        };

        AlertEvent evt = wireName switch
        {
            "alert:acknowledged" => new AlertAcknowledgedEvent { Severity = common.Severity, Title = common.Title },
            "alert:resolved" => new AlertResolvedEvent { Severity = common.Severity, Title = common.Title },
            _ => new AlertCreatedEvent { Severity = common.Severity, Title = common.Title },
        };

        return evt with
        {
            FiredAt = common.FiredAt,
            FiredAtDisplay = common.FiredAtDisplay,
            Url = common.Url,
            ServiceName = ctx.ServiceName,
            CheckName = ctx.CheckName,
            CurrentStatus = ToStatusLabel(ctx.CurrentStatus),
            Description = ctx.AlertDescription,
            AlertId = ctx.AlertId,
            CheckId = ctx.CheckId,
            Value = ctx.AlertValue,
            FailureThreshold = ctx.FailureThreshold,
            SuccessThreshold = ctx.SuccessThreshold,
            ServiceUrl = ctx.ServiceUrl,
            CheckUrl = ctx.CheckUrl,
            IsExternal = ctx.IsExternal,
            SourceLabel = ctx.SourceLabel,
            SourceUrl = ctx.SourceUrl,
        };
    }

    /// <summary>Builds the neutral event for an incident context and the wire name that fired (incident:created/resolved).</summary>
    public static Event ToEvent(this IncidentNotificationContext ctx, string wireName)
    {
        IncidentEvent evt = wireName == "incident:resolved"
            ? new IncidentResolvedEvent { Severity = EventSeverity.Info, Title = ctx.Title }
            : new IncidentCreatedEvent { Severity = EventSeverity.Info, Title = ctx.Title };

        return evt with
        {
            FiredAt = ctx.OccurredAt,
            IncidentId = ctx.IncidentId,
            Status = ctx.Status.ToString(),
            Visibility = ctx.Visibility.ToString(),
            AffectedServices = ctx.AffectedServices,
        };
    }

    private static EventSeverity ToEventSeverity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => EventSeverity.Critical,
        AlertSeverity.Warning => EventSeverity.Warning,
        _ => EventSeverity.Info,
    };

    private static string ToStatusLabel(ServiceStatus status) => status switch
    {
        ServiceStatus.UP => "Up",
        ServiceStatus.DEGRADED => "Degraded",
        ServiceStatus.DOWN => "Down",
        ServiceStatus.MAINTENANCE => "Maintenance",
        ServiceStatus.FAILURE => "Failure",
        _ => "No data",
    };
}
