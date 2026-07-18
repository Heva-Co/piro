using Piro.Domain.Enums;

namespace Piro.Application.Models;

public static class AlertNotificationContextExtensions
{
    /// <summary>
    /// One meaningful line identifying what this alert is about, for use as a notification
    /// title/subject — "{Check} on {Service}" for an internal alert, or "{SourceLabel} alert" (falling
    /// back to the alert description if the source has no label) for an external one, so a
    /// third-party alert never renders as the confusing "External / External".
    /// </summary>
    public static string Title(this AlertNotificationContext ctx)
    {
        if (!ctx.IsExternal)
            return $"{ctx.CheckName} on {ctx.ServiceName}";

        if (ctx.SourceLabel is not null)
            return $"{ctx.SourceLabel} alert";

        return ctx.AlertDescription ?? "External alert";
    }
}

/// <summary>Contextual data passed to a <see cref="Interfaces.ITriggerDispatcher"/> when an alert fires or recovers.</summary>
public record AlertNotificationContext(
    /// <summary>Name of the service that owns the check.</summary>
    string ServiceName,
    /// <summary>Name of the check that triggered the alert.</summary>
    string CheckName,
    /// <summary>Current status that caused the alert.</summary>
    ServiceStatus CurrentStatus,
    /// <summary>Human-readable description of the alert config (optional).</summary>
    string? AlertDescription,
    /// <summary>Severity level from the alert config.</summary>
    AlertSeverity Severity,
    /// <summary>True when the alert is recovering (transitioning from alerting → healthy).</summary>
    bool IsRecovery,
    DateTimeOffset FiredAt,
    int AlertId = 0,
    int CheckId = 0,
    string? AlertValue = null,
    int FailureThreshold = 1,
    int SuccessThreshold = 1,
    string? IncidentUrl = null,
    /// <summary>Absolute admin URL to the service's detail page, if the site URL is configured.</summary>
    string? ServiceUrl = null,
    /// <summary>Absolute admin URL to the check's detail page, if the site URL is configured.</summary>
    string? CheckUrl = null,
    /// <summary>Absolute admin URL to the alert's detail page, if the site URL is configured.</summary>
    string? AlertUrl = null,
    /// <summary>
    /// <see cref="FiredAt"/> pre-formatted for display in the recipient's own time zone, with the
    /// zone name in parentheses (e.g. "2026-07-11 14:32 (America/Bogota)"). Built per-recipient in
    /// <c>EscalationCheckerService.BuildContext</c> since each on-call user may have a different
    /// <see cref="Piro.Domain.Entities.AppUser.TimeZone"/> — never derive display time from
    /// <see cref="FiredAt"/> directly in a dispatcher/template.
    /// </summary>
    string? FiredAtDisplay = null,
    /// <summary>
    /// True for an alert with no Check/Service to correlate against (RFC 0001) — a third-party
    /// alert received via webhook. When true, <see cref="ServiceName"/>/<see cref="CheckName"/>
    /// hold a display placeholder ("External"), not a real name — templates should prefer
    /// <see cref="SourceLabel"/> instead of showing "External / External".
    /// </summary>
    bool IsExternal = false,
    /// <summary>Display label for the alert's origin (e.g. "GCP Cloud Monitoring") — null for an internal alert.</summary>
    string? SourceLabel = null,
    /// <summary>Deep link into the source system's own console for this occurrence, if the source provided one.</summary>
    string? SourceUrl = null
) : INotificationContent;
