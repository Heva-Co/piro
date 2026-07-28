using FluentAssertions;
using NSubstitute;
using Piro.Application.Config;
using Piro.Application.Interfaces;
using Piro.Checks;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.UnitTests.Config;

/// <summary>
/// Covers ConfigExporter (RFC 0019 §4.8). Export is a bootstrap tool and must be honest about being
/// lossy: whatever it cannot represent has to be commented rather than dropped, or a user who
/// exports and then runs `apply --prune` deletes the checks that merely failed to serialize.
/// </summary>
public class ConfigExporterTests
{
    private readonly IServiceRepository _services = Substitute.For<IServiceRepository>();
    private readonly ICheckRepository _checks = Substitute.For<ICheckRepository>();
    private readonly IAlertConfigRepository _alerts = Substitute.For<IAlertConfigRepository>();
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly ICheckRegistry _registry = Substitute.For<ICheckRegistry>();
    private readonly ConfigExporter _sut;

    public ConfigExporterTests()
    {
        _registry.Find("HTTP").Returns(new HttpCheck());
        _tags.GetRequiredWorkerTagsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _alerts.GetByCheckIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _checks.GetByServiceIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _sut = new ConfigExporter(_services, _checks, _alerts, _tags, _registry);
    }

    private void GivenService(Service service, params Check[] checks)
    {
        _services.GetAllAsync(Arg.Any<CancellationToken>()).Returns([service]);
        _checks.GetByServiceIdAsync(service.Id, Arg.Any<CancellationToken>()).Returns(checks);
    }

    private static Service Service(string slug) =>
        new() { Id = 1, Slug = slug, Name = slug };

    private static Check HttpCheckRow(string slug, string typeData = """{"url":"https://a.test"}""") =>
        new()
        {
            Id = 10, ServiceId = 1, Slug = slug, Name = slug,
            Type = CheckType.HTTP, Cron = "*/5 * * * *", TypeDataJson = typeData, IsActive = true,
        };

    [Fact]
    public async Task EmitsTheSchemaCommentAndVersion()
    {
        GivenService(Service("api"));

        var yaml = await _sut.ExportAsync();

        yaml.Should().StartWith("# yaml-language-server: $schema=");
        yaml.Should().Contain("version: 1");
    }

    [Fact]
    public async Task OmitsFieldsAtTheirDefault()
    {
        // Keeps the output readable, and keeps the file a partial assertion rather than a
        // restatement of every default.
        GivenService(Service("api"), HttpCheckRow("health"));

        var yaml = await _sut.ExportAsync();

        yaml.Should().NotContain("is_hidden");
        yaml.Should().NotContain("display_order");
        yaml.Should().NotContain("default_status");
        yaml.Should().NotContain("is_active");
    }

    [Fact]
    public async Task QuotesACronSoItIsNotAYamlAlias()
    {
        // "* * * * *" unquoted is a YAML alias, not a string.
        GivenService(Service("api"), HttpCheckRow("health"));

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("""cron: "*/5 * * * *" """.TrimEnd());
    }

    [Fact]
    public async Task CheckBoundToAnIntegrationColumn_IsCommentedNotDropped()
    {
        var check = HttpCheckRow("gcp-job");
        check.IntegrationId = Guid.NewGuid();
        GivenService(Service("api"), check);

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("# Check 'gcp-job'");
        yaml.Should().Contain("prune");
        yaml.Should().NotContain("- slug: gcp-job");
    }

    [Fact]
    public async Task CheckWhoseManifestRequiresAnIntegration_IsAlsoCommented()
    {
        // The regression this guards, found running export against a real instance: the GCP Cloud
        // Run Job check carries its integration reference inside type_data rather than in the
        // IntegrationId column, so keying only on the column exported a check the validator then
        // rejected — a file that failed its own plan.
        var gcp = Substitute.For<ICheck>();
        gcp.CheckId.Returns("GCP_CloudRunJob");
        gcp.Manifest.Returns(new CheckManifest
        {
            Label = "GCP Cloud Run Job",
            Description = "Cloud Run job",
            ConfigType = typeof(object),
            RequiredIntegration = "GoogleCloud",
        });
        _registry.Find("GCP_CloudRunJob").Returns(gcp);

        var check = HttpCheckRow("job");
        check.Type = CheckType.GCP_CloudRunJob;
        check.TypeDataJson = """{"integrationInstanceId":"c376c1be-e762-447d-a720-e01ff0c0855b"}""";
        GivenService(Service("api"), check);

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("GoogleCloud");
        yaml.Should().NotContain("- slug: job");
        yaml.Should().NotContain("integrationInstanceId");
    }

    [Fact]
    public async Task ConfigThatItsOwnTypeWouldReject_IsFlaggedInPlace()
    {
        // Checks created before type_data was validated exist; exporting them silently produces a
        // file that fails its own plan (§8).
        GivenService(Service("api"), HttpCheckRow("health", """{"url":"not-a-url"}"""));

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("does not match the check type's schema");
    }

    [Fact]
    public async Task AlertConfigsAreExportedWithNonDefaultFieldsOnly()
    {
        var check = HttpCheckRow("health");
        GivenService(Service("api"), check);
        _alerts.GetByCheckIdAsync(check.Id, Arg.Any<CancellationToken>()).Returns([
            new AlertConfig
            {
                CheckId = check.Id, Dimension = "Status", AlertValue = "DOWN",
                FailureThreshold = 3, SuccessThreshold = 1, MinFailingRegions = 1,
                Severity = AlertSeverity.Critical, IsActive = true,
            },
        ]);

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("alert_configs:");
        yaml.Should().Contain("dimension: Status");
        yaml.Should().Contain("failure_threshold: 3");
        yaml.Should().Contain("severity: Critical");
        // Derived from the dimension spec, never written by the file.
        yaml.Should().NotContain("comparison:");
        yaml.Should().NotContain("direction:");
        yaml.Should().NotContain("is_alerting");
        // At their defaults.
        yaml.Should().NotContain("success_threshold");
        yaml.Should().NotContain("min_failing_regions");
    }

    [Fact]
    public async Task EmptyCollectionsAreWrittenInFlowStyle()
    {
        // A bare "headers:" with nothing under it parses back as null, not as empty, so a re-applied
        // export would replace {} with null on every field the check left empty — an update the plan
        // reports and the user never asked for. Found running export against a real instance.
        GivenService(Service("api"), HttpCheckRow("health",
            """{"url":"https://a.test","headers":{},"expectedStatusCodes":[]}"""));

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("headers: {}");
        yaml.Should().Contain("expectedStatusCodes: []");
    }

    [Fact]
    public async Task AnEmptyInstanceExportsAValidDocument()
    {
        _services.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var yaml = await _sut.ExportAsync();

        yaml.Should().Contain("version: 1");
        yaml.Should().Contain("services: []");
    }
}
