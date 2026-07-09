using FluentAssertions;
using Piro.Application.Models;
using Piro.Application.Models.TypeData;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

public class DnsCheckExecutorTests
{
    private static CheckExecutionResult Up(double latency = 20) => new(ServiceStatus.UP, latency, null);
    private static CheckExecutionResult Down(double latency = 20) => new(ServiceStatus.DOWN, latency, "timeout");

    private static DnsCheckData Data(int? degradedAfter = null, int? downAfter = null,
        int? degradedLatencyMs = null, int? downLatencyMs = null) =>
        new()
        {
            Host = "example.com",
            DegradedAfter = degradedAfter,
            DownAfter = downAfter,
            DegradedLatencyMs = degradedLatencyMs,
            DownLatencyMs = downLatencyMs,
        };

    [Fact]
    public void AllNsUp_ReturnsUp()
    {
        var results = new[] { Up(), Up(), Up() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.Status.Should().Be(ServiceStatus.UP);
    }

    [Fact]
    public void OneNsFails_DefaultDegradedAfter1_ReturnsDegraded()
    {
        var results = new[] { Up(), Down(), Up() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data(degradedAfter: 1));

        result.Status.Should().Be(ServiceStatus.DEGRADED);
    }

    [Fact]
    public void AllNsFail_ReturnsDown()
    {
        var results = new[] { Down(), Down(), Down() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.Status.Should().Be(ServiceStatus.DOWN);
    }

    [Fact]
    public void TwoNsFail_DownAfterTwo_ReturnsDown()
    {
        var results = new[] { Up(), Down(), Down() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data(degradedAfter: 1, downAfter: 2));

        result.Status.Should().Be(ServiceStatus.DOWN);
    }

    [Fact]
    public void OneNsFails_DownAfterTwo_ReturnsDegraded()
    {
        var results = new[] { Up(), Down(), Up() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data(degradedAfter: 1, downAfter: 2));

        result.Status.Should().Be(ServiceStatus.DEGRADED);
    }

    [Fact]
    public void HighLatency_ExceedsDegradedThreshold_ReturnsDegraded()
    {
        var results = new[] { Up(latency: 500) };
        var ns = new List<string> { "8.8.8.8" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data(degradedLatencyMs: 300));

        result.Status.Should().Be(ServiceStatus.DEGRADED);
        result.ErrorMessage.Should().Contain("300 ms");
    }

    [Fact]
    public void HighLatency_ExceedsDownThreshold_ReturnsDown()
    {
        var results = new[] { Up(latency: 1200) };
        var ns = new List<string> { "8.8.8.8" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data(degradedLatencyMs: 300, downLatencyMs: 1000));

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.ErrorMessage.Should().Contain("1000 ms");
    }

    [Fact]
    public void ErrorMessages_IncludeFailingNsAddresses()
    {
        var results = new[] { Up(), Down(), Down() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.ErrorMessage.Should().Contain("1.1.1.1");
        result.ErrorMessage.Should().Contain("9.9.9.9");
        result.ErrorMessage.Should().NotContain("8.8.8.8");
    }
}
