using FluentAssertions;
using Piro.Checks;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies each check's <see cref="CheckManifest"/> (RFC 0016) — the single source of truth for
/// per-check metadata, replacing the old <c>CheckType</c> enum + <c>[CheckTypeManifest]</c> attribute.
/// Metadata now lives on the check class itself (<see cref="ICheck.Manifest"/>).
/// </summary>
public class CheckTypeManifestTests
{
    private static readonly ICheck[] AllChecks =
    [
        new HttpCheck(), new DnsCheck(), new TcpCheck(), new PingCheck(),
        new SslCheck(), new GrpcCheck(),
    ];

    [Fact]
    public void EveryCheck_DeclaresLabelDescriptionConfigAndInterval()
    {
        foreach (var check in AllChecks)
        {
            var manifest = check.Manifest;
            manifest.Label.Should().NotBeNullOrEmpty($"{check.CheckId} should declare a label");
            manifest.Description.Should().NotBeNullOrEmpty();
            manifest.ConfigType.Should().NotBeNull();
            manifest.DefaultIntervalSeconds.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Http_ManifestPointsAtHttpCheckConfig()
    {
        new HttpCheck().Manifest.ConfigType.Should().Be(typeof(HttpCheckConfig));
    }

    [Fact]
    public void Ssl_ManifestDeclaresCertExpiryDimension()
    {
        new SslCheck().Manifest.Dimensions.Select(d => d.Name)
            .Should().BeEquivalentTo(["Status", "CertExpiry"]);
    }

    [Fact]
    public void Http_ManifestDeclaresLatencyDimension()
    {
        new HttpCheck().Manifest.Dimensions.Select(d => d.Name)
            .Should().Contain(["Status", "Latency"]);
    }

    [Fact]
    public void ManifestConfigType_ProducesASchemaViaConfigSchemaBuilder()
    {
        // The manifest's ConfigType is the bridge into the shared config-schema engine.
        var schema = ConfigSchemaBuilder.For(new HttpCheck().Manifest.ConfigType);
        schema.Should().Contain(f => f.Key == "url");
    }
}
