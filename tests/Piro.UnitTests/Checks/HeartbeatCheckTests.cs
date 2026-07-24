using FluentAssertions;
using NSubstitute;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="HeartbeatCheck"/> (RFC 0013): the probe reads its own last point and reports UP while
/// within the expected+grace window, DOWN when overdue, and Error (→ NO_DATA, non-alerting) before the
/// first ping. And <see cref="HeartbeatInboundHandler"/>: token presence/validity drive the outcome. The
/// check reports raw state only — it never decides severity.
/// </summary>
public class HeartbeatCheckTests
{
    private static ICheckHost HostWith(IOwnCheckPoints points)
    {
        var host = Substitute.For<ICheckHost>();
        host.GetRequiredService<IOwnCheckPoints>().Returns(points);
        return host;
    }

    private static IOwnCheckPoints PointsWithLatest(CheckPoint? latest)
    {
        var p = Substitute.For<IOwnCheckPoints>();
        p.LatestAsync(Arg.Any<CancellationToken>()).Returns(latest);
        return p;
    }

    private static long NowMinus(int seconds) => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds;

    [Fact]
    public async Task NoPingYet_IsError_NotDown()
    {
        var result = await new HeartbeatCheck().ProbeAsync(new HeartbeatCheckConfig(), HostWith(PointsWithLatest(null)));

        result.Outcome.Should().Be(CheckOutcome.Error); // maps to NO_DATA, kept out of alerting
        result.Message.Should().Contain("No heartbeat");
    }

    [Fact]
    public async Task RecentPing_WithinWindow_IsUp()
    {
        var config = new HeartbeatCheckConfig { ExpectedIntervalSeconds = 60, GracePeriodSeconds = 30 };
        var last = new CheckPoint(NowMinus(45), "UP", new Dictionary<string, double>()); // 45s ago ≤ 90s window
        var result = await new HeartbeatCheck().ProbeAsync(config, HostWith(PointsWithLatest(last)));

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task OverduePing_IsDown_WithMessage()
    {
        var config = new HeartbeatCheckConfig { ExpectedIntervalSeconds = 60, GracePeriodSeconds = 30 };
        var last = new CheckPoint(NowMinus(200), "UP", new Dictionary<string, double>()); // 200s ago > 90s window
        var result = await new HeartbeatCheck().ProbeAsync(config, HostWith(PointsWithLatest(last)));

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("No heartbeat in");
    }

    [Fact]
    public void Manifest_DeclaresStatus_AndConsumesCheckPoints_AndShipsInboundHandler()
    {
        var check = new HeartbeatCheck();

        check.Manifest.ConsumesCheckPoints.Should().BeTrue();
        check.Manifest.Dimensions.Select(d => d.Name).Should().Contain("Status");
        check.ProvidedInboundHandler().Should().BeOfType<HeartbeatInboundHandler>();
    }

    // ── Inbound handler ──────────────────────────────────────────────────────

    private static (ICheckHost host, ICheckTokenValidator validator, ICheckPingRecorder recorder) InboundHost(bool tokenValid)
    {
        var validator = Substitute.For<ICheckTokenValidator>();
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(tokenValid);
        var recorder = Substitute.For<ICheckPingRecorder>();
        var host = Substitute.For<ICheckHost>();
        host.GetRequiredService<ICheckTokenValidator>().Returns(validator);
        host.GetRequiredService<ICheckPingRecorder>().Returns(recorder);
        return (host, validator, recorder);
    }

    private static CheckInboundContext CtxWithToken(string? token) =>
        new(Rest: "", RawBody: "",
            Query: token is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["token"] = token },
            Headers: new Dictionary<string, string>());

    [Fact]
    public async Task Inbound_MissingToken_IsAuthFailed()
    {
        var (host, _, recorder) = InboundHost(tokenValid: true);

        var outcome = await new HeartbeatInboundHandler().HandleAsync(CtxWithToken(null), host);

        outcome.Should().Be(CheckInboundOutcome.AuthFailed);
        await recorder.DidNotReceive().RecordPingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inbound_BadToken_IsAuthFailed_NoPingRecorded()
    {
        var (host, _, recorder) = InboundHost(tokenValid: false);

        var outcome = await new HeartbeatInboundHandler().HandleAsync(CtxWithToken("hb_wrong"), host);

        outcome.Should().Be(CheckInboundOutcome.AuthFailed);
        await recorder.DidNotReceive().RecordPingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Inbound_ValidToken_RecordsPing_AndAccepts()
    {
        var (host, _, recorder) = InboundHost(tokenValid: true);

        var outcome = await new HeartbeatInboundHandler().HandleAsync(CtxWithToken("hb_good"), host);

        outcome.Should().Be(CheckInboundOutcome.Accepted);
        await recorder.Received(1).RecordPingAsync(Arg.Any<CancellationToken>());
    }
}
