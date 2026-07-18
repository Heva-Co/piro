using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Domain.Checks.Config;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies the CheckType manifest (RFC 0011) — the single source of truth for per-CheckType
/// metadata, mirroring the Integration manifest.
/// </summary>
public class CheckTypeManifestTests
{
    [Fact]
    public void RunnableTypes_DeclareDisplayNameDescriptionConfigAndInterval()
    {
        foreach (var type in new[] { CheckType.HTTP, CheckType.DNS, CheckType.TCP, CheckType.Ping, CheckType.SSL, CheckType.GCP_CloudRunJob })
        {
            var manifest = type.GetManifest();
            manifest.Should().NotBeNull($"{type} should declare a manifest");
            manifest!.DisplayName.Should().NotBeNullOrEmpty();
            manifest.Description.Should().NotBeNullOrEmpty();
            manifest.ConfigType.Should().NotBeNull();
            manifest.MinIntervalSeconds.Should().BeGreaterThan(0);
        }
    }

    [Theory]
    [InlineData(CheckType.Heartbeat)]
    [InlineData(CheckType.GRPC)]
    public void NotYetImplementedTypes_HaveNoManifest(CheckType type)
    {
        type.GetManifest().Should().BeNull();
    }

    [Fact]
    public void Http_ManifestPointsAtHttpCheckConfig()
    {
        CheckType.HTTP.GetManifest()!.ConfigType.Should().Be(typeof(HttpCheckConfig));
    }

    [Fact]
    public void GcpCloudRunJob_RequiresGoogleCloudIntegration()
    {
        CheckType.GCP_CloudRunJob.GetManifest()!.RequiredIntegration.Should().Be(IntegrationType.GoogleCloud);
    }

    [Fact]
    public void TypesWithoutRequiredIntegration_ReturnNull()
    {
        CheckType.HTTP.GetManifest()!.RequiredIntegration.Should().BeNull();
    }

    [Fact]
    public void AllowedAlertFors_IsSourcedFromTheManifest()
    {
        CheckType.SSL.AllowedAlertFors().Should().BeEquivalentTo([AlertFor.Status, AlertFor.CertExpiry]);
        CheckType.HTTP.AllowedAlertFors().Should().BeEquivalentTo([AlertFor.Status, AlertFor.Latency]);
    }

    [Fact]
    public void ManifestConfigType_ProducesASchemaViaConfigSchemaBuilder()
    {
        // The manifest's ConfigType is the bridge into the shared config-schema engine.
        var schema = ConfigSchemaBuilder.For(CheckType.HTTP.GetManifest()!.ConfigType);
        schema.Should().Contain(f => f.Key == "url");
    }
}
