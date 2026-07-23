using Piro.Domain.Attributes;
using Piro.Contracts;

namespace Piro.Domain.Enums;

public static class AlertSourceExtensions
{
    /// <summary>
    /// The IntegrationType that produces this AlertSource, for reusing its manifest's Label/IconifyIcon
    /// in the admin UI (e.g. <see cref="AlertSourceExtensions"/>'s callers) instead of duplicating
    /// display metadata per-source. Null for <see cref="AlertSource.Internal"/> — it has no Integration.
    /// </summary>
    public static IntegrationType? ToIntegrationType(this AlertSource source) => source switch
    {
        AlertSource.GcpCloudMonitoring => IntegrationType.GcpCloudMonitoringWebhook,
        _ => null,
    };

    /// <summary>Display label for this Source's origin Integration type (e.g. "GCP Cloud Monitoring") — null for Internal.</summary>
    public static string? GetSourceLabel(this AlertSource source) => source.ToIntegrationType()?.GetManifest()?.Label;

    /// <summary>Iconify icon for this Source's origin Integration type — null for Internal.</summary>
    public static string? GetSourceIconifyIcon(this AlertSource source) => source.ToIntegrationType()?.GetManifest()?.IconifyIcon;
}
