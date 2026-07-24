using FluentAssertions;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="TcpCheck"/> (RFC 0016). TCP owns its own socket, so it needs no service from the
/// host — a host that throws on any resolution is a fine stand-in. A missing host/port is an Error (the
/// check can't run); a refused/timed-out connection is a Down (a real target failure).
/// </summary>
public class TcpCheckExecutorTests
{
    private sealed class ThrowingHost : ICheckHost
    {
        public T GetRequiredService<T>() where T : notnull =>
            throw new InvalidOperationException($"TCP check must not resolve {typeof(T).Name}.");
    }

    private static readonly ThrowingHost _host = new();

    private static Task<CheckProbeResult> Probe(TcpCheckConfig config) =>
        new TcpCheck().ProbeAsync(config, _host);

    [Fact]
    public async Task Returns_Error_When_Host_Not_Configured()
    {
        var result = await Probe(new() { Host = "", Port = 80 });

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
        // Port 1 on localhost is nearly always closed.
        var result = await Probe(new() { Host = "127.0.0.1", Port = 1, TimeoutMs = 2000 });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }

    [Fact]
    public async Task Returns_Down_On_Timeout()
    {
        // 192.0.2.1 is a TEST-NET address — packets are dropped, not refused.
        var result = await Probe(new() { Host = "192.0.2.1", Port = 9999, TimeoutMs = 500 });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }
}
