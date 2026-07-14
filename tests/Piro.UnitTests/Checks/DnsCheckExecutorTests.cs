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

    private static DnsCheckData Data() => new() { Host = "example.com" };

    [Fact]
    public void AllNsUp_ReturnsUp_WithZeroFailedNameServers()
    {
        var results = new[] { Up(), Up(), Up() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.Status.Should().Be(ServiceStatus.UP);
        result.MetricValue.Should().Be(0);
    }

    [Fact]
    public void OneNsFails_StillReturnsUp_ButReportsFailedNameServerCount()
    {
        // Severity ("is 1 failed NS a problem?") is no longer judged by the executor
        // (RFC 0002) — it's up to an AlertConfig on FailedNameServers to decide.
        var results = new[] { Up(), Down(), Up() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.Status.Should().Be(ServiceStatus.UP);
        result.MetricValue.Should().Be(1);
    }

    [Fact]
    public void AllNsFail_ReturnsDown()
    {
        var results = new[] { Down(), Down(), Down() };
        var ns = new List<string> { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

        var result = DnsCheckExecutor.ClassifyNsResults(results, ns, Data());

        result.Status.Should().Be(ServiceStatus.DOWN);
        result.MetricValue.Should().Be(3);
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
