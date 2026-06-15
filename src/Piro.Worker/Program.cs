using Piro.Infrastructure.Extensions;
using Piro.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, services, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console(
              outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    })
    .ConfigureServices((ctx, services) =>
    {
        // Only the check executors — no DB, no Quartz, no alerts
        services.AddWorkerInfrastructure(ctx.Configuration);
        services.AddHostedService<WorkerSignalRService>();
    })
    .Build();

await host.RunAsync();
