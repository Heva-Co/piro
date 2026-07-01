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
                return new CheckExecutionResult(ServiceStatus.DOWN, null, "Host is not configured.");

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

                if (expiresIn <= TimeSpan.Zero)
                    return new CheckExecutionResult(ServiceStatus.DOWN, sw.Elapsed.TotalMilliseconds,
                        $"Certificate expired on {cert.NotAfter:yyyy-MM-dd}.");

                if (expiresIn.TotalDays < data.WarningDaysBeforeExpiry)
                    return new CheckExecutionResult(ServiceStatus.DEGRADED, sw.Elapsed.TotalMilliseconds,
                        $"Certificate expires in {(int)expiresIn.TotalDays} day(s) ({cert.NotAfter:yyyy-MM-dd}).");

                return new CheckExecutionResult(ServiceStatus.UP, sw.Elapsed.TotalMilliseconds, null);
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
}
