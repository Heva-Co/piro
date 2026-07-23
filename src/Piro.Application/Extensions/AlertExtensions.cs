using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

/// <summary>Display-safe accessors for an orphan-capable Alert (RFC 0001) — Check/Service may be null.</summary>
public static class AlertExtensions
{
    // Which integration a third-party AlertSource originates from, and its display metadata, is
    // integration knowledge — it lives here in the Application layer, not in Piro.Domain (RFC 0016).
    // AlertSource itself stays a pure domain enum; only this mapping knows about integrations.
    private static string? GetSourceLabel(this AlertSource source) => source switch
    {
        AlertSource.GcpCloudMonitoring => "GCP Cloud Monitoring",
        _ => null,
    };

    private static string? GetSourceIconifyIcon(this AlertSource source) => source switch
    {
        AlertSource.GcpCloudMonitoring => "logos:google-cloud",
        _ => null,
    };

    /// <summary>Maps a lightweight list-view row to its wire DTO, including Source's display metadata.</summary>
    public static AlertSummaryDto ToDto(this AlertSummaryRow r) => new(
        r.Id, r.CheckSlug, r.CheckName, r.ServiceSlug, r.ServiceName,
        r.AlertConfigDescription, r.Message, r.ImpactAtFireTime,
        r.FiredAt, r.ResolvedAt, r.OccurrenceCount, r.IncidentId, r.HasEscalationPolicy, r.Source,
        r.Source.GetSourceLabel(), r.Source.GetSourceIconifyIcon());

    /// <summary>Maps a full detail-view row to its wire DTO, including Source's display metadata.</summary>
    public static AlertDetailDto ToDto(this AlertDetailRow row) => new(
        row.Id, row.CheckSlug, row.CheckName, row.ServiceSlug, row.ServiceName,
        row.AlertConfigId, row.AlertFor, row.AlertValue, row.FailureThreshold, row.SuccessThreshold,
        row.AlertConfigDescription, row.Message, row.ImpactAtFireTime, row.Severity,
        row.FiredAt, row.ResolvedAt, row.OccurrenceCount, row.IncidentId, row.IncidentTitle,
        row.EscalationCurrentStep, row.EscalationExhaustedAt, row.AcknowledgedAt, row.AcknowledgedBy, row.Source,
        row.Source.GetSourceLabel(), row.Source.GetSourceIconifyIcon(),
        row.SourceRawPayload, row.SourceUrl);

    /// <summary>The Service's display name — only meaningful for an internal, anchored alert. See <see cref="AlertNotificationContext.IsExternal"/>.</summary>
    public static string ServiceLabel(this Alert alert) => alert.Service?.Name ?? "External";

    /// <summary>The Check's display name — only meaningful for an internal, anchored alert. See <see cref="AlertNotificationContext.IsExternal"/>.</summary>
    public static string CheckLabel(this Alert alert) => alert.Check?.Name ?? "External";

    /// <summary>The alert's severity, from its config; defaults to Critical for an orphan alert with no config.</summary>
    public static AlertSeverity SeverityOrDefault(this Alert alert) => alert.AlertConfig?.Severity ?? AlertSeverity.Critical;

    /// <summary>True for a third-party alert with no correlated Check/Service (RFC 0001).</summary>
    public static bool IsExternal(this Alert alert) => alert.Service is null && alert.Check is null;

    /// <summary>The origin label for an external alert (e.g. "GCP Cloud Monitoring"); null for an internal one.</summary>
    public static string? ExternalSourceLabel(this Alert alert) =>
        alert.Source == AlertSource.Internal ? null : alert.Source.GetSourceLabel();

    /// <summary>
    /// Builds the notification context passed to an <see cref="Piro.Application.Interfaces.IPersonalNotificationDispatcher{TContent}"/>
    /// for this alert's on-call escalation (see EscalationCheckerService). <paramref name="firedAtDisplay"/>
    /// is pre-formatted for the specific recipient's time zone by the caller — each on-call user may
    /// have a different <see cref="Domain.Entities.AppUser.TimeZone"/>, so it can't be derived here.
    /// </summary>
    public static AlertNotificationContext ToNotificationContext(
        this Alert alert, string? serviceUrl, string? checkUrl, string? alertUrl, string? firedAtDisplay) => new(
        ServiceName: alert.ServiceLabel(),
        CheckName: alert.CheckLabel(),
        CurrentStatus: alert.ImpactAtFireTime,
        AlertDescription: alert.AlertConfig?.Description ?? alert.Message,
        Severity: alert.AlertConfig?.Severity ?? AlertSeverity.Critical,
        IsRecovery: false,
        FiredAt: alert.FiredAt,
        AlertId: alert.Id,
        CheckId: alert.CheckId ?? 0,
        ServiceUrl: serviceUrl,
        CheckUrl: checkUrl,
        AlertUrl: alertUrl,
        FiredAtDisplay: firedAtDisplay,
        IsExternal: alert.Service is null && alert.Check is null,
        SourceLabel: alert.Source.GetSourceLabel(),
        SourceUrl: alert.SourceUrl
    );
}
