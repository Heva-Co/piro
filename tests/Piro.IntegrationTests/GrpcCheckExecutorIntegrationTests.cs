using FluentAssertions;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.IntegrationTests;

/// <summary>
/// Exercises <see cref="GrpcCheckExecutor"/> against a real in-process gRPC server that implements
/// the standard health checking protocol (<c>grpc.health.v1.Health</c>), hosted on a plaintext (h2c)
/// Kestrel endpoint on a dynamic port. No Docker or external network — fully deterministic in CI.
/// </summary>
public class GrpcCheckExecutorIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HealthServiceImpl _health = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _health = new HealthServiceImpl();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            // Port 0 → OS assigns a free port; force HTTP/2 cleartext (h2c) so no TLS is involved.
            // Must bind an explicit loopback IP (not "localhost") for dynamic-port binding.
            options.Listen(System.Net.IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2);
        });
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(_health);
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.MapGrpcService<HealthServiceImpl>();

        await _app.StartAsync();

        var address = _app.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
            .Addresses.First();
        _port = new Uri(address).Port;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private Check MakeCheck(string service) => new()
    {
        Id = 1, Slug = "grpc", Name = "grpc",
        Type = CheckType.GRPC,
        TypeDataJson = $"{{\"tls\": false, \"host\": \"localhost\", \"port\": {_port}, \"service\": \"{service}\", \"timeout\": 5000}}",
    };

    [Fact]
    public async Task Serving_Service_Returns_Up()
    {
        _health.SetStatus("piro.Test", HealthCheckResponse.Types.ServingStatus.Serving);

        var result = await new GrpcCheckExecutor().ExecuteAsync(MakeCheck("piro.Test"));

        result.Status.Should().Be(ServiceStatus.UP);
        result.LatencyMs.Should().NotBeNull();
    }

    [Fact]
    public async Task NotServing_Service_Returns_Down()
    {
        _health.SetStatus("piro.Test", HealthCheckResponse.Types.ServingStatus.NotServing);

        var result = await new GrpcCheckExecutor().ExecuteAsync(MakeCheck("piro.Test"));

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("NotServing");
    }

    [Fact]
    public async Task Empty_Service_Uses_Overall_Server_Health()
    {
        // The empty service name is the conventional "overall server health" key.
        _health.SetStatus("", HealthCheckResponse.Types.ServingStatus.Serving);

        var result = await new GrpcCheckExecutor().ExecuteAsync(MakeCheck(""));

        result.Status.Should().Be(ServiceStatus.UP);
    }

    [Fact]
    public async Task Unknown_Service_Returns_Down()
    {
        // A service the server has never registered → the health service reports NotFound → DOWN.
        var result = await new GrpcCheckExecutor().ExecuteAsync(MakeCheck("does.not.Exist"));

        result.Status.Should().Be(ServiceStatus.DOWN);
    }
}
