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
        var fields = new AlertFields(
            Severity: ToEventSeverity(ctx.Severity),
            Title: ctx.Title(),
            ServiceName: ctx.ServiceName,
            CheckName: ctx.CheckName);

        return wireName switch
        {
            "alert:acknowledged" => Fill(new AlertAcknowledgedEvent { Severity = fields.Severity, Title = fields.Title, ServiceName = fields.ServiceName, CheckName = fields.CheckName }, ctx),
            "alert:resolved" => Fill(new AlertResolvedEvent { Severity = fields.Severity, Title = fields.Title, ServiceName = fields.ServiceName, CheckName = fields.CheckName }, ctx),
            _ => Fill(new AlertCreatedEvent { Severity = fields.Severity, Title = fields.Title, ServiceName = fields.ServiceName, CheckName = fields.CheckName }, ctx),
        };
    }

    private readonly record struct AlertFields(
        EventSeverity Severity, string Title, string ServiceName, string CheckName);

    /// <summary>Copies the optional alert fields onto an already-required-initialized event via a non-destructive <c>with</c>.</summary>
    private static AlertEvent Fill(AlertEvent evt, AlertNotificationContext ctx) => evt with
    {
        FiredAt = ctx.FiredAt,
        FiredAtDisplay = ctx.FiredAtDisplay,
        Url = ctx.AlertUrl,
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
