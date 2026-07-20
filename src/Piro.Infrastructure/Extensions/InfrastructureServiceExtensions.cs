using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Alerts;
using Piro.Infrastructure.Auth;
using Piro.Infrastructure.Email;
using Piro.Infrastructure.Checks;
using Piro.Infrastructure.Integrations.GoogleCloud;
using Piro.Infrastructure.Integrations.OAuth;
using Piro.Infrastructure.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Infrastructure.Hubs;
using Piro.Infrastructure.Jobs;
using Piro.Infrastructure.Notifications;
using Piro.Infrastructure.Notifications.Subscribers;
using Piro.Infrastructure.Persistence;
using Piro.Infrastructure.Persistence.Repositories;
using Piro.Infrastructure.Workers;
using Quartz;


namespace Piro.Infrastructure.Extensions;

/// <summary>Registers all infrastructure services into the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>Adds EF Core (PostgreSQL or SQLite based on connection string prefix), repositories, and schedulers.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<PiroDbContext>(opts => opts.UseNpgsql(connectionString));

        // ASP.NET Core Identity
        services.AddIdentity<AppUser, AppRole>(opts =>
            {
                opts.Password.RequireDigit = false;
                opts.Password.RequireLowercase = false;
                opts.Password.RequireUppercase = false;
                opts.Password.RequireNonAlphanumeric = false;
                opts.Password.RequiredLength = 8;
                opts.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<PiroDbContext>()
            .AddDefaultTokenProviders();

        // Auth infrastructure services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<IEmailConfigRepository, EmailConfigRepository>();
        services.AddScoped<SmtpEmailService>();
        services.AddScoped<ResendEmailService>();
        services.AddScoped<IEmailService, EmailServiceFactory>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IOidcConfigRepository, OidcConfigRepository>();
        services.AddScoped<IOidcService, OidcService>();
        services.AddDistributedMemoryCache();
        services.AddDataProtection();
        services.AddHttpClient("oidc-http", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = System.Net.HttpVersion.Version11;
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<ICheckRepository, CheckRepository>();
        services.AddScoped<IServiceDependencyRepository, ServiceDependencyRepository>();
        services.AddScoped<ICheckDataPointRepository, CheckDataPointRepository>();
services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();

        // Check executors — registered as ICheckExecutor so CheckRunnerService can inject IEnumerable<ICheckExecutor>
        services.AddHttpClient("piro-http");
        services.AddHttpClient("piro-http-noredirect")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Dedicated client for alert webhook dispatching — HTTP/1.1 forced, explicit timeout
        services.AddHttpClient("piro-webhook", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = System.Net.HttpVersion.Version11;
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                // Force IPv4 — .NET may hang trying IPv6 on some networks
                ConnectCallback = async (context, ct) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                },
            });

        services.AddScoped<ICheckExecutor, HttpCheckExecutor>();
        services.AddScoped<ICheckExecutor, PingCheckExecutor>();
        services.AddScoped<ICheckExecutor, TcpCheckExecutor>();
        services.AddScoped<ICheckExecutor, DnsCheckExecutor>();
        services.AddScoped<ICheckExecutor, SslCheckExecutor>();
        services.AddScoped<ICheckExecutor, GrpcCheckExecutor>();
        services.AddScoped<ICheckExecutor, GcpCloudRunJobCheckExecutor>();
        services.AddSingleton<GcpTokenCache>();
        services.AddScoped<IGcpTokenProvider, GcpTokenProvider>();

        // In-process event pipeline: check executions → service status recomputation
        services.AddSingleton(Channel.CreateUnbounded<CheckStatusChangedEvent>());
        services.AddHostedService<StatusDrainHostedService>();

        // Quartz scheduler — persistent PostgreSQL store (survives restarts)
        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseDefaultThreadPool(tp => tp.MaxConcurrency = Environment.ProcessorCount * 2);
            q.UsePersistentStore(s =>
            {
                s.UseProperties = true;
                s.UseNewtonsoftJsonSerializer();
                s.UsePostgres(pg => pg.ConnectionString = connectionString);
                // Clustering intentionally disabled: the API doesn't run multiple replicas today,
                // and the cluster check-in handshake (lock negotiation over QRTZ_LOCKS) adds real
                // startup latency against a remote Postgres. Re-enable if horizontal scaling of
                // the API is ever introduced.
            });

            // Maintenance event status transitions — every 15 minutes
            q.AddJob<MaintenanceSchedulerJob>(j => j.WithIdentity(MaintenanceSchedulerJob.Key).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(MaintenanceSchedulerJob.Key)
                .WithIdentity("maintenance-scheduler-trigger")
                .WithCronSchedule("0 0/15 * * * ?"));

            // Escalation policy step checks — every minute
            q.AddJob<EscalationCheckJob>(j => j.WithIdentity(EscalationCheckJob.Key).StoreDurably());
            q.AddTrigger(t => t
                .ForJob(EscalationCheckJob.Key)
                .WithIdentity("escalation-check-trigger")
                .WithCronSchedule("0 * * * * ?"));

        });
        services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

        services.AddScoped<ICheckSchedulerService, CheckSchedulerService>();
        services.AddSingleton<ICronIntervalCalculator, QuartzCronIntervalCalculator>();
        services.AddScoped<IJobStatusService, JobStatusService>();
        services.AddScoped<IRRuleExpander, RRuleExpander>();

        // Integration repository
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();

        // OAuth integration framework (RFC 0004) — generic OAuth client + encrypted token store.
        services.AddScoped<IOAuthTokenStore, OAuthTokenStore>();
        services.AddScoped<IOAuthClient, OAuthClient>();
        services.AddSingleton<ISecretProtector, DataProtectorSecretProtector>();
        services.AddScoped<IOAuthTokenProvider, OAuthTokenProvider>();
        services.AddScoped<IPagerDutyDiscoveryService, PagerDutyDiscoveryService>();
        services.AddScoped<IServiceIntegrationMappingRepository, ServiceIntegrationMappingRepository>();
        // Provider descriptors — one per third-party OAuth service (resolved as IEnumerable).
        services.AddSingleton<IOAuthProviderDescriptor, PagerDutyOAuthProviderDescriptor>();
        // Dedicated HTTP client for third-party OAuth token endpoints — HTTP/1.1, IPv4-forced (mirrors oidc-http).
        services.AddHttpClient("oauth-integration-http", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = System.Net.HttpVersion.Version11;
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

        // On-call scheduling
        services.AddScoped<IOnCallScheduleRepository, OnCallScheduleRepository>();
        services.AddScoped<OnCallService>();
        services.AddScoped<OnCallScheduleAppService>();

        // Escalation
        services.AddScoped<IEscalationPolicyRepository, EscalationPolicyRepository>();
        services.AddScoped<EscalationCheckerService>();
        services.AddScoped<EscalationPolicyAppService>();

        // User notification preferences
        services.AddScoped<IUserNotificationPreferenceRepository, UserNotificationPreferenceRepository>();

        // Alert repositories
        services.AddScoped<IAlertConfigRepository, AlertConfigRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IWebhookRequestLogRepository, WebhookRequestLogRepository>();

        // Dashboard metrics
        services.AddScoped<IMetricsRepository, MetricsRepository>();

        // Log repository
        services.AddScoped<ILogRepository, LogRepository>();

        // Global search (admin Cmd+K)
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<SearchAppService>();

        // Site config repository
        services.AddScoped<ISiteConfigRepository, SiteConfigRepository>();
        services.AddScoped<ISiteUrlBuilder, SiteUrlBuilder>();

        // Personal notification dispatchers (RFC 0009 mode 1) — registered as
        // IEnumerable<IPersonalNotificationDispatcher<AlertNotificationContext>>. The group-only
        // types (Slack/GoogleChat/Discord/MsTeams/Webhook/Opsgenie) are not registered here: their
        // IChannelNotificationDispatcher.SendAsync is still a phase-5 stub. Pushover is left
        // unregistered exactly as before (phase 1 preserves behavior — it is wired in a later phase).
        services.AddScoped<IPersonalNotificationDispatcher<AlertNotificationContext>, EmailDispatcher>();
        services.AddScoped<IPersonalNotificationDispatcher<AlertNotificationContext>, TelegramDispatcher>();
        services.AddScoped<IPersonalNotificationDispatcher<AlertNotificationContext>, TwilioSmsDispatcher>();
        services.AddScoped<IPersonalNotificationDispatcher<AlertNotificationContext>, NtfyDispatcher>();

        // Channel notification dispatchers (RFC 0009 mode 2) — post to a shared team channel.
        services.AddScoped<IChannelNotificationDispatcher<AlertNotificationContext>, GoogleChatDispatcher>();

        // Incident notification dispatchers (RFC 0009 §4.2, phase 6) — Email (personal) and Google Chat
        // (channel) can carry incident notifications as well as alerts.
        services.AddScoped<IPersonalNotificationDispatcher<IncidentNotificationContext>, EmailDispatcher>();
        services.AddScoped<IChannelNotificationDispatcher<IncidentNotificationContext>, GoogleChatDispatcher>();

        // Verification-code senders (RFC 0009 §4.9) — personal plain-text channels only.
        services.AddScoped<IVerificationCodeSender, EmailDispatcher>();
        services.AddScoped<IVerificationCodeSender, TelegramDispatcher>();
        services.AddScoped<IVerificationCodeSender, TwilioSmsDispatcher>();
        services.AddScoped<IVerificationCodeSender, NtfyDispatcher>();

        // Notification push engine (RFC 0009) — durable outbox + drain worker (phase 3) and the
        // subscription-matching processor (phase 4) that replaces the phase-3 no-op.
        services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();
        services.AddScoped<IAlertNotificationPublisher, AlertNotificationPublisher>();
        services.AddScoped<IIncidentNotificationPublisher, IncidentNotificationPublisher>();
        services.AddScoped<INotificationEventProcessor, SubscriptionMatchingProcessor>();
        services.AddHostedService<NotificationDispatchWorker>();

        // Subscriptions (RFC 0009 §4.4): repository, delivery-log repository, and CRUD app service.
        services.AddScoped<INotificationSubscriptionRepository, NotificationSubscriptionRepository>();
        services.AddScoped<INotificationDeliveryLogRepository, NotificationDeliveryLogRepository>();
        services.AddScoped<NotificationSubscriptionAppService>();

        // Subscriber declarations (RFC 0009 §4.4) — each integration type declares which catalog events it
        // handles and in which delivery mode. This is the menu the subscription UI offers per destination.
        // Email and Google Chat carry incidents too; the others carry alerts only in v1.
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.Email, NotificationTargetKind.Personal, EventSubscriber.AlertAndIncidentEvents));
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.Telegram, NotificationTargetKind.Personal, EventSubscriber.AlertEvents));
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.Twilio, NotificationTargetKind.Personal, EventSubscriber.AlertEvents));
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.Ntfy, NotificationTargetKind.Personal, EventSubscriber.AlertEvents));
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.GoogleChat, NotificationTargetKind.Channel, EventSubscriber.AlertAndIncidentEvents));
        services.AddScoped<INotificationSubscriber>(_ => new EventSubscriber(IntegrationType.PagerDuty, NotificationTargetKind.Integration, EventSubscriber.AlertEvents));

        // System-event dispatchers (RFC 0004) — trigger/resolve to a shared incident channel.
        services.AddScoped<ISystemEventDispatcher, PagerDutyDispatcher>();

        // Worker repositories
        services.AddScoped<IWorkerRegistrationRepository, WorkerRegistrationRepository>();

        // Worker registry — singleton: tracks live SignalR connections in-memory
        services.AddSingleton<IWorkerRegistry, WorkerRegistry>();

        // PIRO_WORKER_REGION: region label for in-process check results (defaults to "default")
        var workerRegion = configuration["PIRO_WORKER_REGION"] ?? "default";

        // LocalCheckJobDispatcher: always available — runs checks in-process when built-in worker is active
        services.AddScoped<LocalCheckJobDispatcher>(sp =>
            new LocalCheckJobDispatcher(
                sp.GetRequiredService<IEnumerable<ICheckExecutor>>(),
                sp.GetRequiredService<ICheckResultIngester>(),
                sp.GetRequiredService<ICheckDataPointRepository>(),
                workerRegion,
                sp.GetRequiredService<ILogger<LocalCheckJobDispatcher>>()));

        // RemoteCheckJobDispatcher: fans out to all connected SignalR workers
        // apiIsWorker is resolved at dispatch time via registry — pass false here, routing handles it
        services.AddScoped<RemoteCheckJobDispatcher>(sp =>
            new RemoteCheckJobDispatcher(
                sp.GetRequiredService<IHubContext<WorkerHub, IWorkerClient>>(),
                sp.GetRequiredService<IWorkerRegistry>(),
                sp.GetRequiredService<IMultiRegionBatchTracker>(),
                sp.GetRequiredService<ICheckDataPointRepository>(),
                sp.GetRequiredService<IEnumerable<ICheckExecutor>>(),
                sp.GetRequiredService<ICheckResultIngester>(),
                apiIsWorker: false,   // multi-region fan-out never includes the built-in API worker directly
                workerRegion,
                sp.GetRequiredService<ILogger<RemoteCheckJobDispatcher>>()));

        // RoutingCheckJobDispatcher: checks registry at dispatch time to decide if built-in API worker is active
        services.AddScoped<ICheckJobDispatcher>(sp =>
            new RoutingCheckJobDispatcher(
                sp.GetRequiredService<LocalCheckJobDispatcher>(),
                sp.GetRequiredService<RemoteCheckJobDispatcher>(),
                sp.GetRequiredService<IWorkerRegistry>()));

        // Built-in API worker: always registered so the DB record + UI entry always exist.
        // Supports runtime enable/disable via ApiWorkerHostedService.Enable()/Disable() without restart.
        services.AddSingleton(sp => new ApiWorkerHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IWorkerRegistry>(),
            workerRegion,
            sp.GetRequiredService<ILogger<ApiWorkerHostedService>>()));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ApiWorkerHostedService>());

        return services;
    }

    /// <summary>
    /// Registers only the check executor services needed by the standalone <c>Piro.Worker</c> process.
    /// Does not register the database, Quartz, Identity, or alert infrastructure.
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("piro-http");
        services.AddHttpClient("piro-http-noredirect")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddScoped<ICheckExecutor, HttpCheckExecutor>();
        services.AddScoped<ICheckExecutor, PingCheckExecutor>();
        services.AddScoped<ICheckExecutor, TcpCheckExecutor>();
        services.AddScoped<ICheckExecutor, DnsCheckExecutor>();
        services.AddScoped<ICheckExecutor, SslCheckExecutor>();
        services.AddScoped<ICheckExecutor, GrpcCheckExecutor>();

        return services;
    }
}
