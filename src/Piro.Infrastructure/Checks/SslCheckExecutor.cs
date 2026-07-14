using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Infrastructure.Checks;

/// <summary>Executes an SSL certificate validity and expiry check.</summary>
internal class SslCheckExecutor : ICheckExecutor
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CheckType CheckType => CheckType.SSL;

    public async Task<CheckExecutionResult> ExecuteAsync(Check check, CancellationToken ct = default)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SslCheckData>(check.TypeDataJson, _json)
                       ?? new SslCheckData();

            if (string.IsNullOrWhiteSpace(data.Host))
                return new CheckExecutionResult(ServiceStatus.FAILURE, null, "Host is not configured.");

            var sw = Stopwatch.StartNew();
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(data.Host, data.Port, ct);

                using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) => cert is not null);
                await ssl.AuthenticateAsClientAsync(data.Host);
                sw.Stop();

                var cert = ssl.RemoteCertificate as X509Certificate2
                           ?? new X509Certificate2(ssl.RemoteCertificate!);

                var expiresIn = cert.NotAfter - DateTime.UtcNow;
                return ClassifyExpiry(expiresIn, cert.NotAfter, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds, ex.Message);
            }
        }
        catch (Exception ex)
        {
            return new CheckExecutionResult(ServiceStatus.FAILURE, null, $"Executor error: {ex.Message}");
        }
    }

    /// <summary>
    /// The check only measures: expired (or handshake failure elsewhere) is the one real DOWN.
    /// Everything else is UP, reporting days-until-expiry as <see cref="CheckExecutionResult.MetricValue"/>
    /// so severity (e.g. "warn at 30 days, critical at 7") is an <see cref="AlertConfig"/>
    /// decision via <see cref="AlertFor.CertExpiry"/> — see RFC 0002.
    /// </summary>
    internal static CheckExecutionResult ClassifyExpiry(TimeSpan expiresIn, DateTime notAfter, double? latencyMs)
    {
        if (expiresIn <= TimeSpan.Zero)
            return new CheckExecutionResult(ServiceStatus.DOWN, latencyMs,
                $"Certificate expired on {notAfter:yyyy-MM-dd}.");

        var daysRemaining = expiresIn.TotalDays;
        return new CheckExecutionResult(ServiceStatus.UP, latencyMs,
            $"Certificate expires in {(int)daysRemaining} day(s) ({notAfter:yyyy-MM-dd}).",
            MetricValue: daysRemaining);
    }
}
