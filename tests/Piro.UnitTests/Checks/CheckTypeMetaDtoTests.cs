using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Checks;
using Piro.Integrations.GoogleCloud;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies CheckTypeManifestExtensions.ToMetaDto — the projection behind GET /api/v1/checks/types
/// (RFC 0016). It maps a registered check's <c>Manifest</c> to the wire DTO: display metadata, its
/// dimensions, the reflected config schema, and the required-integration hint.
/// </summary>
public class CheckTypeMetaDtoTests
{
    [Fact]
    public void Http_ProjectsFullMetadataAndConfigSchema()
    {
        var dto = new HttpCheck().ToMetaDto();

        dto.Should().NotBeNull();
        dto.Type.Should().Be("HTTP");
        dto.DisplayName.Should().Be("HTTP");
        dto.Description.Should().NotBeNullOrEmpty();
        dto.MinIntervalSeconds.Should().Be(60);
        dto.Dimensions.Select(d => d.Name).Should().Contain(["Status", "Latency"]);
        dto.ConfigSchema.Should().Contain(f => f.Key == "url");
        dto.RequiredIntegrationType.Should().BeNull();
        dto.HasExecutor.Should().BeTrue();
    }

    [Fact]
    public void GcpCloudRunJob_ReportsRequiredIntegration()
    {
        var dto = new GcpCloudRunJobCheck().ToMetaDto();

        dto.RequiredIntegrationType.Should().Be("GoogleCloud");
    }

    [Fact]
    public void Ssl_ProjectsCertExpiryDimension()
    {
        var dto = new SslCheck().ToMetaDto();

        dto.Dimensions.Select(d => d.Name).Should().BeEquivalentTo(["Status", "CertExpiry"]);
    }
}
