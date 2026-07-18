using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Domain.Enums;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies CheckTypeManifestExtensions.ToMetaDto — the projection behind GET /api/v1/checks/types
/// (RFC 0011), including the reflected config schema and the HasExecutor flag.
/// </summary>
public class CheckTypeMetaDtoTests
{
    [Fact]
    public void Http_ProjectsFullMetadataAndConfigSchema()
    {
        var dto = CheckType.HTTP.ToMetaDto(hasExecutor: true);

        dto.Should().NotBeNull();
        dto!.Type.Should().Be("HTTP");
        dto.DisplayName.Should().Be("HTTP");
        dto.Description.Should().NotBeNullOrEmpty();
        dto.MinIntervalSeconds.Should().Be(60);
        dto.AllowedAlertFors.Should().BeEquivalentTo(["Status", "Latency"]);
        dto.ConfigSchema.Should().Contain(f => f.Key == "url");
        dto.RequiredIntegrationType.Should().BeNull();
        dto.HasExecutor.Should().BeTrue();
    }

    [Fact]
    public void GcpCloudRunJob_ReportsRequiredIntegration()
    {
        var dto = CheckType.GCP_CloudRunJob.ToMetaDto(hasExecutor: true);

        dto!.RequiredIntegrationType.Should().Be("GoogleCloud");
    }

    [Theory]
    [InlineData(CheckType.Heartbeat)]
    [InlineData(CheckType.GRPC)]
    public void NotYetImplementedTypes_ProjectToNull(CheckType type)
    {
        type.ToMetaDto(hasExecutor: false).Should().BeNull();
    }

    [Fact]
    public void HasExecutorFlag_ReflectsTheArgument()
    {
        CheckType.HTTP.ToMetaDto(hasExecutor: false)!.HasExecutor.Should().BeFalse();
    }
}
