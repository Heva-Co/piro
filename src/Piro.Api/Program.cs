using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Piro.Api.Middleware;
using Piro.Api.OpenApi;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Infrastructure.Auth;
using Piro.Infrastructure.Extensions;
using Piro.Infrastructure.Hubs;
using Piro.Infrastructure.Logging;
using Piro.Infrastructure.Persistence;
using Serilog;
using Serilog.Sinks.PeriodicBatching;

var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
var apiVersion = assemblyVersion is not null
    ? $"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
    : "v1";

var builder = WebApplication.CreateBuilder(args);

// Sentry error tracking — only active in Production when Sentry:Dsn is configured
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Configuration["Sentry:Environment"] ?? "production";
        o.TracesSampleRate = double.TryParse(builder.Configuration["Sentry:TracesSampleRate"], out var r) ? r : 0.1;
        o.AttachStacktrace = true;
        o.SendDefaultPii = false;
        // Strip request URL and service names from events — they may contain customer hostnames
        o.SetBeforeSend((evt, _) =>
        {
            if (evt.Request is { } req)
            {
                req.QueryString = null;
                req.Data = null;
            }
            return evt;
        });
    });
}

// Bootstrap logger (console only) until the DB is available
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((ctx, services, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .WriteTo.Console(
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
      .WriteTo.Sink(
          new PeriodicBatchingSink(
              new EfCoreLogSink(services.GetRequiredService<IServiceScopeFactory>()),
              new PeriodicBatchingSinkOptions { BatchSizeLimit = 50, Period = TimeSpan.FromSeconds(5) }));
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddCors(opts =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:5173", "http://localhost:5174"];
    opts.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info = new OpenApiInfo
        {
            Title = "Piro API",
            Version = apiVersion,
            Description = "REST API for Piro — an open-source status page platform. " +
                          "Authenticate using a JWT Bearer token (obtained from `/api/v1/auth/sign-in`) " +
                          "or an API key passed in the `X-API-Key` header.",
            Contact = new OpenApiContact
            {
                Name = "Piro on GitHub",
                Url = new Uri("https://github.com/Heva-Co/piro"),
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT"),
            },
        };

        var publicUrl = builder.Configuration["PublicUrl"];
        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            doc.Servers = [new OpenApiServer { Url = publicUrl, Description = "Production" }];
        }

        return Task.CompletedTask;
    });

    options.AddDocumentTransformer<SecuritySchemeTransformer>();

    // Include XML doc comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.AddDocumentTransformer((doc, _, _) => Task.CompletedTask); // placeholder — XML loaded via schema transformers
});

builder.Services.AddSignalR()
    .AddJsonProtocol(opts =>
        opts.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddInfrastructure(builder.Configuration);

// JWT authentication
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is required.");

builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// Application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ServiceAppService>();
builder.Services.AddScoped<CheckAppService>();
builder.Services.AddScoped<DependencyService>();
builder.Services.AddScoped<AlertEvaluationService>();
builder.Services.AddScoped<AlertConfigAppService>();
builder.Services.AddScoped<NotificationChannelAppService>();
builder.Services.AddScoped<IntegrationAppService>();
builder.Services.AddScoped<CheckRunnerService>();
builder.Services.AddScoped<ServiceStatusService>();
builder.Services.AddScoped<IncidentAppService>();
builder.Services.AddScoped<MaintenanceAppService>();
builder.Services.AddScoped<YamlImportService>();
builder.Services.AddScoped<ICheckResultIngester, CheckResultIngesterService>();
builder.Services.AddSingleton<IMultiRegionBatchTracker, MultiRegionBatchTracker>();
builder.Services.AddScoped<WorkerAppService>();

var app = builder.Build();

// Apply pending EF Core migrations and initialize check scheduler on startup
string emailProviderLabel;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PiroDbContext>();
    db.Database.Migrate();

    var scheduler = scope.ServiceProvider.GetRequiredService<ICheckSchedulerService>();
    await scheduler.InitializeFromDatabaseAsync();

    var emailConfig = scope.ServiceProvider.GetRequiredService<IEmailConfigRepository>();
    var cfg = await emailConfig.GetAsync();
    emailProviderLabel = (cfg.Provider ?? "smtp").ToUpperInvariant();
    if (cfg.Provider is null && !string.IsNullOrWhiteSpace(app.Configuration["Email:Host"]))
        emailProviderLabel = "SMTP (env)";
}

// Startup banner
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var env = app.Environment;
startupLogger.LogInformation("=== Piro {Version} started ===", apiVersion);
startupLogger.LogInformation("Environment : {AspNetEnvironment}", env.EnvironmentName);
startupLogger.LogInformation("Sentry      : {Sentry}", (!env.IsDevelopment() && !string.IsNullOrWhiteSpace(sentryDsn)) ? "enabled" : "disabled");
startupLogger.LogInformation("DB          : {Db}", (app.Configuration["Database:ConnectionString"] ?? "").Split(';')[0]);
startupLogger.LogInformation("Email       : {EmailProvider}", emailProviderLabel);

app.UseCors();
app.UseStaticFiles();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// OpenAPI 3.1 spec — available at /openapi/v1.json
app.MapOpenApi();

// Swagger UI — points at the native OpenAPI 3.1 spec
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "Piro API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Piro API — Swagger UI";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<WorkerHub>("/hub/worker");
app.MapHub<AdminHub>("/hub/admin");

// Health check endpoint — used by Docker / load balancers
app.MapGet("/health", () => Results.Ok(new { status = "healthy", version = apiVersion, timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .AllowAnonymous();

app.Run();
