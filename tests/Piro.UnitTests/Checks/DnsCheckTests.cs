using FluentAssertions;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Tests <see cref="DnsCheck"/> (RFC 0016): the config guards (missing host / invalid name server /
/// invalid expected value → Error), the manifest, and the pure per-record matching helpers
/// (<c>MatchScalar</c>, <c>MatchMxRecords</c>), exposed to tests via InternalsVisibleTo.
/// </summary>
public class DnsCheckTests
{
    private sealed class ThrowingHost : ICheckHost
    {
        public T GetRequiredService<T>() where T : notnull =>
            throw new InvalidOperationException($"DNS check must not resolve {typeof(T).Name}.");
    }

    private static readonly ThrowingHost _host = new();

    [Fact]
    public async Task Returns_Error_When_Host_Not_Configured()
    {
        var result = await new DnsCheck().ProbeAsync(new DnsCheckConfig { Host = "" }, _host);

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("Host is not configured");
    }

    [Fact]
    public async Task Returns_Error_When_NameServer_Is_Invalid()
    {
        var config = new DnsCheckConfig
        {
            Host = "example.com",
            NameServers = ["not a valid ns!!"],
        };

        var result = await new DnsCheck().ProbeAsync(config, _host);

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("Invalid name server");
    }

    [Fact]
    public async Task Returns_Error_When_ExpectedValue_Is_Invalid_For_RecordType()
    {
        // An A record expects a valid IPv4 literal; a hostname there is a config error, not a target outage.
        var config = new DnsCheckConfig
        {
            Host = "example.com",
            RecordType = "A",
            ExpectedValue = "not-an-ip",
        };

        var result = await new DnsCheck().ProbeAsync(config, _host);

        result.Outcome.Should().Be(CheckOutcome.Error);
        result.Message.Should().Contain("Invalid expected value");
    }

    [Fact]
    public void Manifest_ExposesStatusLatencyAndFailedNameServersDimensions()
    {
        var manifest = new DnsCheck().Manifest;

        manifest.Label.Should().Be("DNS");
        manifest.ConfigType.Should().Be(typeof(DnsCheckConfig));
        manifest.Dimensions.Select(d => d.Name)
            .Should().Contain(["Status", "Latency", "FailedNameServers"]);
    }

    [Fact]
    public void MatchScalar_A_Record_MatchesExactIp_CaseSensitive()
    {
        DnsCheck.MatchScalar("A", "1.2.3.4", ["1.2.3.4"]).Should().BeNull();
        DnsCheck.MatchScalar("A", "1.2.3.4", ["5.6.7.8"]).Should().Contain("not found in A records");
    }

    [Fact]
    public void MatchScalar_Cname_IsCaseInsensitive_And_TrimsExpectedTrailingDot()
    {
        // ExtractValues already trims the trailing dot off actual records, so a match is case-insensitive
        // on the host and tolerant of a trailing dot in the configured expected value.
        DnsCheck.MatchScalar("CNAME", "Target.Example.Com.", ["target.example.com"]).Should().BeNull();
    }

    [Fact]
    public void MatchMxRecords_MatchesHost_IgnoringPriority_WhenPriorityNotSet()
    {
        var expected = new[] { new MxExpectation { Exchange = "mx1.example.com" } };
        var actual = new[] { ("mx1.example.com.", 10) };

        DnsCheck.MatchMxRecords(expected, actual).Should().BeNull();
    }

    [Fact]
    public void MatchMxRecords_RequiresPriority_WhenSet()
    {
        var expected = new[] { new MxExpectation { Exchange = "mx1.example.com", Priority = 5 } };
        var actual = new[] { ("mx1.example.com", 10) };

        DnsCheck.MatchMxRecords(expected, actual).Should().Contain("priority 5");
    }
}
