using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.Checks;

/// <summary>
/// Probes a TLS certificate. Down only when the certificate is expired or the handshake fails; otherwise
/// Up, reporting days-until-expiry as the metric so the policy decides warn/critical windows (RFC 0002).
/// </summary>
public sealed class SslCheck : Check<SslCheckConfig>
{
    public override string CheckId => "SSL";

    /// <summary>Days remaining until the certificate expires — fewer days is worse.</summary>
    private static readonly DimensionSpec CertExpiry = new("CertExpiry", DimensionComparison.Threshold, ThresholdDirection.LowerIsWorse, "days");

    public override CheckManifest Manifest => new()
    {
        Label = "SSL",
        Description = "Check a TLS certificate's validity and expiry.",
        ConfigType = typeof(SslCheckConfig),
        Dimensions = [CommonDimensions.Status, CertExpiry],
    };

    public override async Task<CheckProbeResult> ProbeAsync(SslCheckConfig config, ICheckHost host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            return CheckProbeResult.Failed("Host is not configured.");

        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(config.Host, config.Port, ct);

            using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) => cert is not null);
            await ssl.AuthenticateAsClientAsync(config.Host);
            sw.Stop();

            var cert = ssl.RemoteCertificate as X509Certificate2
                       ?? new X509Certificate2(ssl.RemoteCertificate!);

            return ClassifyExpiry(cert.NotAfter - DateTime.UtcNow, cert.NotAfter, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CheckProbeResult.DownWith(ex.Message,
                CommonDimensions.Latency.Measure(sw.Elapsed.TotalMilliseconds));
        }
    }

    /// <summary>Expired is the only real Down; otherwise Up with days-until-expiry as a LowerIsWorse dimension.</summary>
    internal static CheckProbeResult ClassifyExpiry(TimeSpan expiresIn, DateTime notAfter, double latencyMs)
    {
        var latency = CommonDimensions.Latency.Measure(latencyMs);
        if (expiresIn <= TimeSpan.Zero)
            return CheckProbeResult.DownWith($"Certificate expired on {notAfter:yyyy-MM-dd}.", latency);

        return CheckProbeResult.Ok(latency, CertExpiry.Measure(expiresIn.TotalDays));
    }
}
