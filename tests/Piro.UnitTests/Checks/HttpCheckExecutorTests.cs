using System.Net;
using FluentAssertions;
using NSubstitute;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="HttpCheck"/> logic (RFC 0016) using a fake HttpMessageHandler — no real network. The
/// check resolves its <see cref="IHttpClientFactory"/> from the <see cref="ICheckHost"/>, so a fake host
/// hands it a factory wired to the fake handler. The check returns a raw <see cref="CheckOutcome"/> and a
/// set of dimensions; it never decides severity (a failed soft rule stays Up but bumps the
/// "BodyRuleFailures" dimension, which the policy — not the check — turns into DEGRADED).
/// </summary>
public class HttpCheckExecutorTests
{
    private static ICheckHost HostWith(HttpStatusCode statusCode, string? body = null) =>
        HostWithHandler(new FakeHandler(statusCode, body));

    private static ICheckHost HostWithHandler(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        var host = Substitute.For<ICheckHost>();
        host.GetRequiredService<IHttpClientFactory>().Returns(factory);
        return host;
    }

    private static Task<CheckProbeResult> Probe(ICheckHost host, HttpCheckConfig config) =>
        new HttpCheck().ProbeAsync(config, host);

    [Fact]
    public async Task Returns_Up_When_Status200_And_No_ExpectedCodes()
    {
        var result = await Probe(HostWith(HttpStatusCode.OK), new() { Url = "http://example.com" });

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task Returns_Down_When_Status500()
    {
        var result = await Probe(HostWith(HttpStatusCode.InternalServerError), new() { Url = "http://example.com" });

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Returns_Up_When_Status_Matches_ExpectedStatusCodes()
    {
        var result = await Probe(HostWith(HttpStatusCode.NotFound),
            new() { Url = "http://example.com", ExpectedStatusCodes = ["404"] });

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task Returns_Down_When_Status_Not_In_ExpectedStatusCodes()
    {
        var result = await Probe(HostWith(HttpStatusCode.OK),
            new() { Url = "http://example.com", ExpectedStatusCodes = ["201"] });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }

    [Fact]
    public async Task Returns_Up_When_Body_Contains_Rule_Passes()
    {
        var result = await Probe(HostWith(HttpStatusCode.OK, "Hello, world!"),
            new() { Url = "http://example.com", ResponseRules = [new() { Type = "contains", Value = "world", Degraded = false }] });

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task Returns_Down_When_Body_Contains_Rule_Fails()
    {
        var result = await Probe(HostWith(HttpStatusCode.OK, "Hello, world!"),
            new() { Url = "http://example.com", ResponseRules = [new() { Type = "contains", Value = "piro", Degraded = false }] });

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("body");
    }

    [Fact]
    public async Task Soft_Rule_Failure_Stays_Up_But_Counts_A_BodyRuleFailure()
    {
        // "Degraded" is no longer a check-level status (RFC 0016) — a failed soft rule keeps the probe Up
        // and bumps the BodyRuleFailures dimension, which the alert policy turns into DEGRADED, not the check.
        var result = await Probe(HostWith(HttpStatusCode.OK, "Hello, world!"),
            new() { Url = "http://example.com", ResponseRules = [new() { Type = "contains", Value = "piro", Degraded = true }] });

        result.Outcome.Should().Be(CheckOutcome.Up);
        result.Dimensions.Single(d => d.Name == "BodyRuleFailures").Value.Should().Be(1);
    }

    [Fact]
    public async Task Returns_Up_When_JsonPath_Rule_Passes()
    {
        var body = """{"status":{"indicator":"none"}}""";
        var result = await Probe(HostWith(HttpStatusCode.OK, body),
            new() { Url = "http://example.com", ResponseRules = [new() { Type = "json_path", Value = "$.status.indicator", Expected = "none", Degraded = false }] });

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task Returns_Down_When_JsonPath_Expected_Value_Mismatches()
    {
        var body = """{"status":{"indicator":"major"}}""";
        var result = await Probe(HostWith(HttpStatusCode.OK, body),
            new() { Url = "http://example.com", ResponseRules = [new() { Type = "json_path", Value = "$.status.indicator", Expected = "none", Degraded = false }] });

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("major");
    }

    [Fact]
    public async Task Up_Result_Reports_Latency_Dimension_Regardless_Of_How_Slow_The_Response_Is()
    {
        // A slow-but-successful response is still Up; the Latency dimension is what the policy alerts on.
        var host = HostWithHandler(new DelayedHandler(HttpStatusCode.OK, delayMs: 50));
        var result = await Probe(host, new() { Url = "http://example.com" });

        result.Outcome.Should().Be(CheckOutcome.Up);
        result.Dimensions.Single(d => d.Name == "Latency").Value.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task Returns_Up_When_Status_Matches_2xx_Class()
    {
        var result = await Probe(HostWith(HttpStatusCode.Created),
            new() { Url = "http://example.com", ExpectedStatusCodes = ["2xx"] });

        result.Outcome.Should().Be(CheckOutcome.Up);
    }

    [Fact]
    public async Task Returns_Down_When_Status_Does_Not_Match_2xx_Class()
    {
        var result = await Probe(HostWith(HttpStatusCode.NotFound),
            new() { Url = "http://example.com", ExpectedStatusCodes = ["2xx"] });

        result.Outcome.Should().Be(CheckOutcome.Down);
    }

    [Fact]
    public async Task Returns_Error_When_Url_Not_Configured()
    {
        var result = await Probe(HostWith(HttpStatusCode.OK), new() { Url = "" });

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("URL");
    }

    [Fact]
    public async Task Returns_Down_On_Connection_Error()
    {
        var host = HostWithHandler(new ThrowingHandler(new HttpRequestException("Connection refused")));
        var result = await Probe(host, new() { Url = "http://10.0.0.1" });

        result.Outcome.Should().Be(CheckOutcome.Down);
        result.Message.Should().Contain("Connection refused");
    }

    // ── Fake handlers ────────────────────────────────────────────────────────

    private sealed class FakeHandler(HttpStatusCode statusCode, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
                response.Content = new StringContent(body);
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(ex);
    }

    private sealed class DelayedHandler(HttpStatusCode statusCode, int delayMs) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(delayMs, ct);
            return new HttpResponseMessage(statusCode);
        }
    }
}
