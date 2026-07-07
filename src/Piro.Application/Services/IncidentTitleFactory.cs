using Piro.Domain.Enums;

namespace Piro.Application.Services;

/// <summary>Generates incident titles from the check type that triggered them.</summary>
public static class IncidentTitleFactory
{
    public static string Build(CheckType type) => type switch
    {
        CheckType.HTTP         => "HTTP check failing",
        CheckType.DNS          => "DNS resolution failing",
        CheckType.TCP          => "TCP connection failing",
        CheckType.Ping         => "Host unreachable",
        CheckType.SSL          => "SSL certificate issue",
        CheckType.Heartbeat    => "Heartbeat missing",
        CheckType.GCP_CloudRunJob => "Cloud Run job failing",
        _                      => "Service check failing",
    };
}
