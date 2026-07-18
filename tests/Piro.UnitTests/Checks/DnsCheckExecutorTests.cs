using FluentAssertions;
using Piro.Application.Models;
using Piro.Domain.Checks.Config;
using Piro.Domain.Enums;
using Piro.Infrastructure.Checks;

namespace Piro.UnitTests.Checks;

public class DnsCheckExecutorTests
{
    private static CheckExecutionResult Up(double latency = 20) => new(ServiceStatus.UP, latency, null);
    private static CheckExecutionResult Down(double latency = 20) => new(ServiceStatus.DOWN, latency, "timeout");

    private static DnsCheckConfig Data() => new() { Host = "example.com" };

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

    // --- Scalar expected-value matching (A/AAAA/CNAME/TXT/NS/PTR) --------------------------------

    [Theory]
    [InlineData("A", "93.184.216.34", new[] { "93.184.216.34" })]
    [InlineData("AAAA", "2606:2800:220:1:248:1893:25c8:1946", new[] { "2606:2800:220:1:248:1893:25c8:1946" })]
    public void MatchScalar_IpTypes_MatchExactly(string recordType, string expected, string[] actual)
    {
        DnsCheckExecutor.MatchScalar(recordType, expected, actual).Should().BeNull();
    }

    [Fact]
    public void MatchScalar_IpTypes_ReturnErrorWhenMissing()
    {
        var error = DnsCheckExecutor.MatchScalar("A", "1.2.3.4", new[] { "93.184.216.34" });
        error.Should().Contain("1.2.3.4").And.Contain("A records");
    }

    [Theory]
    [InlineData("CNAME", "target.example.com", "TARGET.EXAMPLE.COM")]
    [InlineData("NS", "ns1.example.com", "ns1.example.com")]
    [InlineData("PTR", "host.example.com", "host.example.com")]
    [InlineData("TXT", "v=spf1 include:_spf.google.com ~all", "v=spf1 include:_spf.google.com ~all")]
    public void MatchScalar_NameAndTextTypes_MatchCaseInsensitively(string recordType, string expected, string actual)
    {
        DnsCheckExecutor.MatchScalar(recordType, expected, new[] { actual }).Should().BeNull();
    }

    [Fact]
    public void MatchScalar_NameTypes_IgnoreTrailingDotOnExpected()
    {
        // ExtractValues strips the dot on actuals; MatchScalar strips it on the expected value.
        DnsCheckExecutor.MatchScalar("CNAME", "target.example.com.", new[] { "target.example.com" })
            .Should().BeNull();
    }

    [Fact]
    public void MatchScalar_Txt_MatchesOneOfSeveralStrings()
    {
        var actual = new[] { "google-site-verification=abc", "v=spf1 ~all" };
        DnsCheckExecutor.MatchScalar("TXT", "v=spf1 ~all", actual).Should().BeNull();
    }

    [Fact]
    public void MatchScalar_ReturnsTypedError_WhenNoneMatch()
    {
        DnsCheckExecutor.MatchScalar("NS", "ns9.example.com", new[] { "ns1.example.com" })
            .Should().Contain("name server").And.Contain("ns9.example.com");
    }

    // --- Structured MX matching -----------------------------------------------------------------

    private static (string, int)[] Mx(params (string, int)[] records) => records;

    [Fact]
    public void MatchMxRecords_HostOnly_MatchesIgnoringPriority()
    {
        var expected = new[] { new MxExpectation { Exchange = "mx1.example.com" } };
        var actual = Mx(("mx1.example.com", 20));

        DnsCheckExecutor.MatchMxRecords(expected, actual).Should().BeNull();
    }

    [Fact]
    public void MatchMxRecords_WithPriority_RequiresPriorityToMatch()
    {
        var expected = new[] { new MxExpectation { Exchange = "mx1.example.com", Priority = 10 } };

        DnsCheckExecutor.MatchMxRecords(expected, Mx(("mx1.example.com", 10))).Should().BeNull();

        var error = DnsCheckExecutor.MatchMxRecords(expected, Mx(("mx1.example.com", 20)));
        error.Should().Contain("mx1.example.com").And.Contain("priority 10");
    }

    [Fact]
    public void MatchMxRecords_AllExpectedMustBePresent()
    {
        var expected = new[]
        {
            new MxExpectation { Exchange = "mx1.example.com" },
            new MxExpectation { Exchange = "mx2.example.com" },
        };

        // Only one of the two is present → miss.
        var error = DnsCheckExecutor.MatchMxRecords(expected, Mx(("mx1.example.com", 10)));
        error.Should().Contain("mx2.example.com");

        // Both present → pass.
        DnsCheckExecutor.MatchMxRecords(expected, Mx(("mx1.example.com", 10), ("mx2.example.com", 20)))
            .Should().BeNull();
    }

    [Fact]
    public void MatchMxRecords_HostComparisonIsCaseInsensitiveAndDotTolerant()
    {
        var expected = new[] { new MxExpectation { Exchange = "MX1.Example.COM." } };
        DnsCheckExecutor.MatchMxRecords(expected, Mx(("mx1.example.com", 10))).Should().BeNull();
    }
}
