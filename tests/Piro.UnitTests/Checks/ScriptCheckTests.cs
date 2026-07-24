using System.Net;
using System.Text;
using FluentAssertions;
using Jint;
using Jint.Native;
using NSubstitute;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="ScriptCheck"/> (RFC 0010): the config guard, the manifest, the raw return→outcome
/// mapping (up/down + dimensions, and the "broken script is Error, not Down" rule), and the SSRF guard's
/// blocked-range classification. The check reports raw state only — it never returns DEGRADED; severity
/// is the alert policy's from the emitted dimensions.
/// </summary>
public class ScriptCheckTests
{
    private sealed class ThrowingHost : ICheckHost
    {
        public T GetRequiredService<T>() where T : notnull =>
            throw new InvalidOperationException($"not expected: {typeof(T).Name}");
    }

    // Builds a real JsValue for a JS object literal, so MapReturn is tested against genuine Jint values.
    private static JsValue Js(string objectLiteral)
    {
        var engine = new Engine();
        return engine.Evaluate($"({objectLiteral})");
    }

    [Fact]
    public async Task Returns_Error_When_Script_Empty()
    {
        var result = await new ScriptCheck().ProbeAsync(new ScriptCheckConfig { Script = "" }, new ThrowingHost());

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public void Manifest_DeclaresStatusAndLatency_AndFiveMinuteFloor()
    {
        var manifest = new ScriptCheck().Manifest;

        manifest.Label.Should().Be("Script");
        manifest.ConfigType.Should().Be(typeof(ScriptCheckConfig));
        manifest.Dimensions.Select(d => d.Name).Should().Contain(["Status", "Latency"]);
        manifest.DefaultIntervalSeconds.Should().Be(300);
    }

    [Fact]
    public void MapReturn_Up_IsUp_WithLatencyDimension()
    {
        var result = ScriptCheck.MapReturn(Js("{ up: true }"), latencyMs: 42);

        result.Outcome.Should().Be(CheckOutcome.Up);
        result.Dimensions.Should().ContainSingle(d => d.Name == "Latency" && d.Value == 42);
    }

    [Fact]
    public void MapReturn_Down_IsDown_WithMessage()
    {
        var result = ScriptCheck.MapReturn(Js("{ up: false, message: 'boom' }"), latencyMs: 10);

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Be("boom");
    }

    [Fact]
    public void MapReturn_AttachesNumericDimensions()
    {
        var result = ScriptCheck.MapReturn(Js("{ up: true, dimensions: { Severity: 2, ErrorRate: 0.5 } }"), latencyMs: 5);

        result.Dimensions.Should().Contain(d => d.Name == "Severity" && d.Value == 2);
        result.Dimensions.Should().Contain(d => d.Name == "ErrorRate" && d.Value == 0.5);
    }

    [Fact]
    public void MapReturn_NonObject_IsError()
    {
        ScriptCheck.MapReturn(Js("'nope'"), 1).Outcome.Should().Be(CheckOutcome.Error);
    }

    [Fact]
    public void MapReturn_MissingUp_IsError()
    {
        var result = ScriptCheck.MapReturn(Js("{ message: 'no up field' }"), 1);

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("up");
    }

    [Theory]
    [InlineData("127.0.0.1")]      // loopback
    [InlineData("10.0.0.5")]       // RFC-1918
    [InlineData("172.16.9.9")]     // RFC-1918
    [InlineData("192.168.1.1")]    // RFC-1918
    [InlineData("169.254.169.254")]// cloud metadata
    [InlineData("::1")]            // IPv6 loopback
    [InlineData("fc00::1")]        // IPv6 unique-local
    [InlineData("fe80::1")]        // IPv6 link-local
    public void SsrfGuard_Blocks_PrivateAndMetadata(string ip)
    {
        ScriptSsrfGuard.IsBlocked(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]  // example.com
    public void SsrfGuard_Allows_PublicAddresses(string ip)
    {
        ScriptSsrfGuard.IsBlocked(IPAddress.Parse(ip)).Should().BeFalse();
    }

    // A sync-capable fake handler — ScriptHttp uses HttpClient.Send (the JS call site is synchronous).
    private sealed class SyncFakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(Send(request, ct));
    }

    private static ICheckHost HostReturning(HttpStatusCode status, string body)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(new SyncFakeHandler(status, body)));
        var host = Substitute.For<ICheckHost>();
        host.GetRequiredService<IHttpClientFactory>().Returns(factory);
        return host;
    }

    [Fact]
    public async Task EndToEnd_ScriptDrivesHttp_AndReturnsVerdict()
    {
        // The Stripe-shaped example: read the body, branch, emit a Severity dimension. Exercises the whole
        // engine + piro:http module + return mapping, not just MapReturn.
        const string script = """
            import http from 'piro:http';
            export function check() {
              const s = http.get('https://status.example.com/api.json').json.status;
              if (s.indicator === 'none') return { up: true, dimensions: { Severity: 0 } };
              return { up: false, message: 'Down: ' + s.description, dimensions: { Severity: 2 } };
            }
            """;
        var host = HostReturning(HttpStatusCode.OK, """{ "status": { "indicator": "major", "description": "API errors" } }""");

        var result = await new ScriptCheck().ProbeAsync(new ScriptCheckConfig { Script = script }, host);

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Be("Down: API errors");
        result.Dimensions.Should().Contain(d => d.Name == "Severity" && d.Value == 2);
        result.Dimensions.Should().Contain(d => d.Name == "Latency");
    }

    [Fact]
    public async Task EndToEnd_DisallowedImport_IsError()
    {
        const string script = "import fs from 'node:fs'; export function check() { return { up: true }; }";

        var result = await new ScriptCheck().ProbeAsync(new ScriptCheckConfig { Script = script }, HostReturning(HttpStatusCode.OK, "{}"));

        result.Outcome.Should().Be(CheckOutcome.Error);
    }
}
