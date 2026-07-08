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
using Piro.Infrastructure.Alerts;
using Piro.Infrastructure.Auth;
using Piro.Infrastructure.Email;
using Piro.Infrastructure.Checks;
using Piro.Infrastructure.Integrations.GoogleCloud;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Piro.Infrastructure.Hubs;
using Piro.Infrastructure.Jobs;
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
        services.AddScoped<IIncidentPublishScheduler, IncidentPublishScheduler>();
        services.AddScoped<IRRuleExpander, RRuleExpander>();

        // Integration repository
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();

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
        services.AddScoped<INotificationChannelRepository, NotificationChannelRepository>();

        // Log repository
        services.AddScoped<ILogRepository, LogRepository>();

        // Site config repository
        services.AddScoped<ISiteConfigRepository, SiteConfigRepository>();

        // Alert dispatchers — registered as IEnumerable<INotificationDispatcher>
        services.AddScoped<INotificationDispatcher, EmailDispatcher>();
        services.AddScoped<INotificationDispatcher, WebhookDispatcher>();
        services.AddScoped<INotificationDispatcher, TelegramDispatcher>();
        services.AddScoped<INotificationDispatcher, SlackDispatcher>();
        services.AddScoped<INotificationDispatcher, TwilioSmsDispatcher>();
        services.AddScoped<INotificationDispatcher, GoogleChatDispatcher>();
        services.AddScoped<INotificationDispatcher, MsTeamsDispatcher>();
        services.AddScoped<INotificationDispatcher, DiscordDispatcher>();
        services.AddScoped<INotificationDispatcher, OpsgenieDispatcher>();
        services.AddScoped<INotificationDispatcher, PushoverDispatcher>();
        services.AddScoped<INotificationDispatcher, NtfyDispatcher>();

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
                workerRegion));

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

        return services;
    }
}
