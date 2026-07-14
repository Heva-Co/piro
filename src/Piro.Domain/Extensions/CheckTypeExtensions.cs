using Piro.Domain.Enums;

namespace Piro.Domain.Extensions;

public static class CheckTypeExtensions
{
    /// <summary>
    /// The <see cref="AlertFor"/> values that make sense for a given <see cref="CheckType"/> —
    /// e.g. a GCP Cloud Run Job check has no latency signal, and only SSL checks report
    /// <see cref="AlertFor.CertExpiry"/>. See RFC 0002 §4.4.
    /// </summary>
    public static AlertFor[] AllowedAlertFors(this CheckType type) => type switch
    {
        CheckType.HTTP => [AlertFor.Status, AlertFor.Latency],
        CheckType.DNS => [AlertFor.Status, AlertFor.Latency, AlertFor.FailedNameServers],
        CheckType.TCP => [AlertFor.Status, AlertFor.Latency],
        CheckType.Ping => [AlertFor.Status, AlertFor.Latency],
        CheckType.SSL => [AlertFor.Status, AlertFor.CertExpiry],
        CheckType.Heartbeat => [AlertFor.Status],
        CheckType.GRPC => [AlertFor.Status, AlertFor.Latency],
        CheckType.GCP_CloudRunJob => [AlertFor.Status],
        _ => throw new NotSupportedException()
    };
}
