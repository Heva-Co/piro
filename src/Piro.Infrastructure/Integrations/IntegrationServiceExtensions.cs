using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;
using Piro.Contracts;
using Piro.Infrastructure.Alerts;
using Piro.Infrastructure.Email;
using Piro.Infrastructure.Integrations.Actions;
using Piro.Infrastructure.Integrations.OAuth;
using Piro.Infrastructure.Integrations.Webhooks;
using Piro.Infrastructure.Persistence.Repositories;
using Piro.Infrastructure.Security;
using Piro.Integrations.Abstractions;

namespace Piro.Infrastructure.Integrations;

/// <summary>
/// Wires the whole integration surface (RFC 0004/0012/0016) in one place: the repository, the OAuth
/// framework, the UI-action registry/host and its executor seams, the explicit compile-time integration
/// registry, every <see cref="IIntegration"/> assembly, their event handlers, and the startup hook that
/// runs each integration's <see cref="IIntegration.Configure"/>. Kept out of the general infrastructure
/// wiring so all integration DI lives next to the integration code it registers.
/// </summary>
public static class IntegrationServiceExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();

        AddOAuthFramework(services);
        AddUIActions(services);
        AddRegistryAndIntegrations(services);
        AddEventHandlers(services);
        AddVerificationSenders(services);

        return services;
    }

    /// <summary>OAuth integration framework (RFC 0004): generic OAuth client + encrypted token store + provider descriptors.</summary>
    private static void AddOAuthFramework(IServiceCollection services)
    {
        services.AddScoped<IOAuthTokenStore, OAuthTokenStore>();
        services.AddScoped<IOAuthClient, OAuthClient>();
        services.AddSingleton<ISecretProtector, DataProtectorSecretProtector>();
        services.AddScoped<IOAuthTokenProvider, OAuthTokenProvider>();
        services.AddScoped<Piro.Integrations.Jira.IJiraDiscoveryService, Piro.Integrations.Jira.JiraDiscoveryService>();
        services.AddSingleton<IOAuthProviderDescriptor, Piro.Integrations.Jira.JiraOAuthProviderDescriptor>();

        // Dedicated HTTP client for third-party OAuth token endpoints — HTTP/1.1, IPv4-forced (mirrors oidc-http).
        services.AddHttpClient("oauth-integration-http", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = HttpVersion.Version11;
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                ConnectCallback = async (context, ct) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                },
            });
    }

    /// <summary>
    /// UI actions (RFC 0012, RFC 0016). The action registry is a singleton populated once at startup: it
    /// doubles as <see cref="IUIExtensionHost"/> (the registrar integrations call from Configure) and
    /// <see cref="IUIActionRegistry"/> (what discovery/execution read). Handlers/options are not
    /// DI-scanned — each integration registers its own in Configure. The executor resolves the target and
    /// persists links through Piro-internal seams (an action never touches either).
    /// </summary>
    private static void AddUIActions(IServiceCollection services)
    {
        services.AddSingleton<UIActionRegistry>();
        services.AddSingleton<IUIExtensionHost>(sp => sp.GetRequiredService<UIActionRegistry>());
        services.AddSingleton<IUIActionRegistry>(sp => sp.GetRequiredService<UIActionRegistry>());
        services.AddScoped<IUIActionTargetService, UIActionTargetService>();
        services.AddScoped<IExternalReferenceStore, ExternalReferenceStore>();

        // Inbound webhooks (RFC 0016): the alert-push seam an inbound integration writes through, and the
        // singleton registry (populated at startup via IWebhookHost) that maps an integration to its
        // handler. The webhooks endpoint resolves the instance from the URL and dispatches to it.
        services.AddScoped<IAlertService, AlertServiceHost>();
        services.AddSingleton<WebhookRegistry>();
        services.AddSingleton<IWebhookHost>(sp => sp.GetRequiredService<WebhookRegistry>());
        services.AddSingleton<IInboundWebhookRegistry>(sp => sp.GetRequiredService<WebhookRegistry>());
    }

    /// <summary>
    /// The explicit compile-time set of integrations Piro was built with (RFC 0016). Each integration is
    /// instantiated once here (they are pure data), given a chance to register its own services via
    /// <see cref="IIntegration.ConfigureServices"/> while the container is still being built, and
    /// registered as a singleton. The runtime <see cref="IntegrationStartupConfigurator"/> later calls
    /// each one's <see cref="IIntegration.Configure"/>. Email stays in Infrastructure (its SMTP transport
    /// is core infra); every other integration lives in its own assembly.
    /// </summary>
    private static void AddRegistryAndIntegrations(IServiceCollection services)
    {
        services.AddScoped<IIntegrationHost, IntegrationHost>();
        services.AddSingleton<IIntegrationRegistry, IntegrationRegistry>();
        // Template engine (Scriban) exposed to integrations so they author bodies as templates.
        services.AddSingleton<ITemplateParser, ScribanTemplateParser>();

        IIntegration[] integrations =
        [
            new Piro.Integrations.Telegram.TelegramIntegration(),
            new Piro.Integrations.Twilio.TwilioIntegration(),
            new Piro.Integrations.Ntfy.NtfyIntegration(),
            new Piro.Integrations.GoogleChat.GoogleChatIntegration(),
            new Piro.Integrations.Webhook.WebhookIntegration(),
            new Piro.Integrations.Jira.JiraIntegration(),
            new Piro.Integrations.Gcp.GcpCloudMonitoringWebhookIntegration(),
            new Piro.Integrations.GoogleCloud.GoogleCloudIntegration(),
            new EmailIntegration(),
        ];

        foreach (var integration in integrations)
        {
            integration.ConfigureServices(services);
            services.AddSingleton<IIntegration>(integration);

            // A provider integration ships its own check(s) (RFC 0016) — register them as ICheck so the
            // check registry picks them up. The check is thus available only because its integration is.
            foreach (var check in integration.ProvidedChecks())
                services.AddSingleton<ICheck>(check);
        }

        services.AddHostedService<IntegrationStartupConfigurator>();
    }

    /// <summary>
    /// Notification event handlers — one <see cref="IIntegrationEventHandler"/> per delivering integration,
    /// against the neutral Event hierarchy. Email stays here (its SMTP transport is core infra).
    /// </summary>
    private static void AddEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IIntegrationEventHandler, EmailDispatcher>();
        services.AddScoped<IIntegrationEventHandler, Piro.Integrations.Telegram.TelegramNotificationDispatcher>();
        services.AddScoped<IIntegrationEventHandler, Piro.Integrations.Twilio.TwilioNotificationDispatcher>();
        services.AddScoped<IIntegrationEventHandler, Piro.Integrations.Ntfy.NtfyNotificationDispatcher>();
        services.AddScoped<IIntegrationEventHandler, Piro.Integrations.GoogleChat.GoogleChatNotificationDispatcher>();
        services.AddScoped<IIntegrationEventHandler, Piro.Integrations.Webhook.WebhookNotificationDispatcher>();
    }

    /// <summary>
    /// Verification-code senders (RFC 0009 §4.9) — a distinct concern from notification dispatch, kept in
    /// Infrastructure. Email plus the personal plain-text channels.
    /// </summary>
    private static void AddVerificationSenders(IServiceCollection services)
    {
        services.AddScoped<IVerificationCodeSender, EmailDispatcher>();
        services.AddScoped<IVerificationCodeSender, Piro.Integrations.Telegram.TelegramNotificationDispatcher>();
        services.AddScoped<IVerificationCodeSender, Piro.Integrations.Twilio.TwilioNotificationDispatcher>();
        services.AddScoped<IVerificationCodeSender, Piro.Integrations.Ntfy.NtfyNotificationDispatcher>();
    }
}
