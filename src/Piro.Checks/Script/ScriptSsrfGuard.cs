using System.Net;
using System.Net.Sockets;

namespace Piro.Checks;

/// <summary>
/// Connect-time SSRF guard for the sandboxed Script check's outbound HTTP (RFC 0010 §4.4). A script is
/// authored by a trusted operator, so this defends against <em>accidental</em> SSRF (a copy-pasted URL
/// pointing at an internal address) and DNS rebinding — not a hostile tenant. It resolves the target
/// host and refuses to connect to loopback, link-local / cloud-metadata, or RFC-1918 private ranges,
/// validating the <em>resolved IP</em> (not the hostname) so a name that resolves public at authoring
/// time but private at run time is still caught at connect.
/// <para>
/// Written as a standalone <see cref="System.Net.Http.SocketsHttpHandler.ConnectCallback"/> so it can be
/// reused to harden the other currently-unguarded outbound clients later (RFC 0010 §6 phase 5).
/// </para>
/// </summary>
internal static class ScriptSsrfGuard
{
    /// <summary>Resolves the endpoint, rejects any blocked address, and connects to the first allowed one.</summary>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;

        // A literal blocked hostname never even resolves out to a public address, so reject by name too.
        if (IsBlockedHostName(host))
            throw new ScriptEgressBlockedException($"Refusing to connect to '{host}': blocked host.");

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        var target = addresses.FirstOrDefault(a => !IsBlocked(a));
        if (target is null)
            throw new ScriptEgressBlockedException(
                $"Refusing to connect to '{host}': it resolves only to private, loopback, or metadata addresses.");

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsBlockedHostName(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the address is one a check must never reach: loopback, link-local/metadata, or private.</summary>
    internal static bool IsBlocked(IPAddress address)
    {
        // Normalize IPv4-mapped IPv6 (::ffff:a.b.c.d) to its IPv4 form so the v4 rules below still catch it.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10                                  // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 (link-local + cloud metadata)
                || b[0] == 0;                                  // 0.0.0.0/8 ("this host")
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal)                       // fe80::/10
                return true;
            var b = address.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;                      // fc00::/7 (unique local addresses)
        }

        return false;
    }
}

/// <summary>Thrown when the SSRF guard refuses an outbound connection; surfaced to the operator as the check message.</summary>
internal sealed class ScriptEgressBlockedException(string message) : Exception(message);
