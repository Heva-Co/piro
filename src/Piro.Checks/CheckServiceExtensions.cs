using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Piro.Checks.Abstractions;

namespace Piro.Checks;

/// <summary>
/// DI registration for the check SDK: every shipped <see cref="ICheck"/>, the <see cref="ICheckRegistry"/>
/// that indexes them, and the <see cref="ICheckHost"/> that gives them their allow-listed window into
/// Piro. Called by both the API and the standalone Worker — they run the same set of checks.
/// </summary>
public static class CheckServiceExtensions
{
    public static IServiceCollection AddChecks(this IServiceCollection services)
    {
        // The checks Piro core ships. Provider-specific checks (e.g. the GCP Cloud Run Job check) are
        // NOT here — each integration contributes its own via IIntegration.ProvidedChecks(), so a check
        // is only available when its integration is registered. The registry merges both sources.
        services.AddSingleton<ICheck, HttpCheck>();
        services.AddSingleton<ICheck, DnsCheck>();
        services.AddSingleton<ICheck, TcpCheck>();
        services.AddSingleton<ICheck, PingCheck>();
        services.AddSingleton<ICheck, SslCheck>();
        services.AddSingleton<ICheck, GrpcCheck>();
        services.AddSingleton<ICheck, ScriptCheck>();

        services.AddSingleton<ICheckRegistry, CheckRegistry>();

        // The Script check's outbound HTTP goes through its own SSRF-guarded client (RFC 0010 §4.4),
        // separate from the shared "piro-http" so existing checks are unaffected. The guard validates the
        // resolved IP at connect time — rejecting loopback, link-local/metadata, and private ranges.
        services.AddHttpClient(ScriptCheck.ScriptHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ConnectCallback = ScriptSsrfGuard.ConnectAsync,
            });

        // Base allow-list: the shared infrastructure the built-in checks may resolve through the host.
        // Integrations that ship a check add their own allowed types (e.g. GoogleCloud → IGcpTokenProvider).
        services.AddSingleton(CheckHostAllowedType.Of<System.Net.Http.IHttpClientFactory>());

        // The host is scoped: it resolves per-request/per-execution services from the ambient scope.
        services.AddScoped<ICheckHost, CheckHost>();

        return services;
    }
}
