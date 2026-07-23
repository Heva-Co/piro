using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Checks.Config;

namespace Piro.Domain.Enums;

/// <summary>
/// Protocol or mechanism used to probe a service. Each runnable value carries a
/// <see cref="CheckTypeManifestAttribute"/> declaring its metadata and config shape (RFC 0011).
/// <see cref="Heartbeat"/> is declared but not yet implemented — it has no executor and no manifest,
/// so <c>GetManifest()</c> returns null for it.
/// </summary>
public enum CheckType
{
    [CheckTypeManifest("HTTP",
        "Fetch a URL and assert on the status code and response body.",
        typeof(HttpCheckConfig), 60,
        [AlertFor.Status, AlertFor.Latency])]
    HTTP,

    [CheckTypeManifest("DNS",
        "Resolve a hostname and assert on the returned records.",
        typeof(DnsCheckConfig), 60,
        [AlertFor.Status, AlertFor.Latency, AlertFor.FailedNameServers])]
    DNS,

    [CheckTypeManifest("TCP",
        "Open a TCP connection to a host and port.",
        typeof(TcpCheckConfig), 60,
        [AlertFor.Status, AlertFor.Latency])]
    TCP,

    [CheckTypeManifest("Ping",
        "Send an ICMP echo to a host.",
        typeof(PingCheckConfig), 60,
        [AlertFor.Status, AlertFor.Latency])]
    Ping,

    [CheckTypeManifest("SSL",
        "Check a TLS certificate's validity and expiry.",
        typeof(SslCheckConfig), 60,
        [AlertFor.Status, AlertFor.CertExpiry])]
    SSL,

    /// <summary>Declared but not implemented — no executor, no manifest (RFC 0011 §8).</summary>
    Heartbeat,

    [CheckTypeManifest("gRPC",
        "Probe a gRPC server via the standard health checking protocol.",
        typeof(GrpcCheckConfig), 60,
        [AlertFor.Status, AlertFor.Latency])]
    GRPC,

    [CheckTypeManifest("GCP Cloud Run Job",
        "Verify a Cloud Run Job has completed within a freshness window.",
        typeof(GcpCloudRunJobCheckConfig), 60,
        [AlertFor.Status],
        RequiresIntegration = "GoogleCloud")]
    GCP_CloudRunJob
}
