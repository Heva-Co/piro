using FluentAssertions;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="GrpcCheck"/> (RFC 0016). The gRPC SDK owns its own transport, so the check needs no
/// service from the host — a host that throws on any resolution is a fine stand-in. A missing host/port
/// is an Error (the check can't run); a refused/timed-out call is a Down (a real target failure).
/// </summary>
public class GrpcCheckExecutorTests
{
    private sealed class ThrowingHost : ICheckHost
    {
        public T GetRequiredService<T>() where T : notnull =>
            throw new InvalidOperationException($"gRPC check must not resolve {typeof(T).Name}.");
    }

    private static readonly ThrowingHost _host = new();

    private static Task<CheckProbeResult> Probe(GrpcCheckConfig config) =>
        new GrpcCheck().ProbeAsync(config, _host);

    [Fact]
    public async Task Returns_Error_When_Host_Not_Configured()
    {
        var result = await Probe(new() { Host = "", Port = 50051 });

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("Host or port");
    }

    [Fact]
    public async Task Returns_Error_When_Port_Is_Zero()
    {
        var result = await Probe(new() { Host = "example.com", Port = 0 });

        result.Outcome.Should().Be(CheckOutcome.Error);
    }

    [Fact]
    public async Task Returns_Down_When_Connection_Refused()
    {
        // Port 1 on localhost is nearly always closed — the channel can't connect.
        var result = await Probe(new() { Host = "127.0.0.1", Port = 1, Tls = false, TimeoutMs = 2000 });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }

    [Fact]
    public async Task Returns_Down_On_Timeout()
    {
        // 192.0.2.1 is a TEST-NET address — packets are dropped, so the call deadlines out.
        var result = await Probe(new() { Host = "192.0.2.1", Port = 50051, Tls = false, TimeoutMs = 500 });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }
}
