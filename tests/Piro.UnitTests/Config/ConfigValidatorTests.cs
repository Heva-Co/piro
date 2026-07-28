using FluentAssertions;
using NSubstitute;
using Piro.Application.Config;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Config;

/// <summary>
/// Covers ConfigValidator (RFC 0019 §4.3) — the guards that must run before anything is written,
/// including the two the CRUD write path does not perform at all: binding type_data to the check's
/// own config type, and checking alert_value against its dimension's comparison.
/// </summary>
public class ConfigValidatorTests
{
    private readonly ICheckRegistry _registry = Substitute.For<ICheckRegistry>();
    private readonly ICronIntervalCalculator _cron = Substitute.For<ICronIntervalCalculator>();
    private readonly ConfigValidator _sut;

    public ConfigValidatorTests()
    {
        var http = new HttpCheck();
        _registry.All.Returns([http]);
        _registry.Find("HTTP").Returns(http);
        _cron.IsValid(Arg.Any<string>()).Returns(true);
        _cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromMinutes(1));

        _sut = new ConfigValidator(_registry, _cron);
    }

    private (IReadOnlyList<ValidatedService> Services, List<ConfigValidationError> Errors) Validate(
        params string[] yamls)
    {
        var errors = new List<ConfigValidationError>();
        var parsed = new List<(ConfigDocumentSource, ConfigDocument)>();

        for (var i = 0; i < yamls.Length; i++)
        {
            var source = new ConfigDocumentSource($"file{i}.yaml", yamls[i]);
            if (ConfigYamlParser.Parse(source, errors) is { } document)
                parsed.Add((source, document));
        }

        return (_sut.Validate(parsed, errors), errors);
    }

    private const string ValidHttpCheck = """
        version: 1
        services:
          - slug: api
            name: API
            checks:
              - slug: health
                name: Health
                type: HTTP
                cron: "* * * * *"
                type_data:
                  url: https://api.heva.com/health
        """;

    [Fact]
    public void AcceptsAValidDocument()
    {
        var (services, errors) = Validate(ValidHttpCheck);

        errors.Should().BeEmpty();
        var service = services.Should().ContainSingle().Subject;
        service.Slug.Should().Be("api");
        service.Checks.Should().ContainSingle().Which.Type.Should().Be(Piro.Domain.Enums.CheckType.HTTP);
    }

    [Fact]
    public void MissingVersion_IsRejected()
    {
        var (_, errors) = Validate("""
            services:
              - slug: api
                name: API
            """);

        errors.Should().ContainSingle().Which.Message.Should().Contain("version: 1");
    }

    [Fact]
    public void UnsupportedVersion_NamesWhatIsUnderstood()
    {
        var (_, errors) = Validate("""
            version: 2
            services: []
            """);

        errors.Should().ContainSingle().Which.Message.Should().Contain("version 1");
    }

    [Fact]
    public void DuplicateServiceAcrossFiles_NamesBothPaths()
    {
        // Files are concatenated, not merged, so a collision has to point at both files or the user
        // is left grepping a directory (§4.6).
        var (_, errors) = Validate(ValidHttpCheck, ValidHttpCheck);

        var error = errors.Should().ContainSingle().Subject;
        error.Message.Should().Contain("file0.yaml").And.Contain("declared twice");
        error.Path.Should().Be("file1.yaml");
    }

    [Fact]
    public void UnknownCheckType_ListsTheRegisteredOnes()
    {
        var (_, errors) = Validate("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: c
                    name: C
                    type: Heartbeat
                    cron: "* * * * *"
            """);

        // Heartbeat is in the CheckType enum but has no registered implementation, so resolving
        // through the registry is what keeps an unusable check out of the database.
        errors.Should().ContainSingle().Which.Message.Should().Contain("HTTP");
    }

    [Fact]
    public void InvalidTypeData_IsCaughtBeforeTheWrite()
    {
        // The CRUD path stores this verbatim and only fails when the check executes (§4.3).
        _cron.IsValid(Arg.Any<string>()).Returns(true);

        var (_, errors) = Validate("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "* * * * *"
                    type_data:
                      url: not-a-url
            """);

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Pointer!.StartsWith("services[0].checks[0].type_data"));
    }

    [Fact]
    public void InvalidCron_IsRejected()
    {
        _cron.IsValid("nonsense").Returns(false);

        var (_, errors) = Validate("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: nonsense
                    type_data:
                      url: https://a.test/health
            """);

        errors.Should().Contain(e => e.Message.Contains("not a valid cron"));
    }

    [Fact]
    public void IntervalBelowTheGlobalFloor_IsRejected()
    {
        _cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromSeconds(30));

        var (_, errors) = Validate(ValidHttpCheck);

        errors.Should().Contain(e => e.Message.Contains("at least 1 minute"));
    }

    [Fact]
    public void TimeoutNotShorterThanInterval_IsRejected()
    {
        _cron.SmallestInterval(Arg.Any<string>()).Returns(TimeSpan.FromMinutes(1));

        var (_, errors) = Validate("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "* * * * *"
                    type_data:
                      url: https://a.test/health
                      timeout: 60000
            """);

        errors.Should().Contain(e => e.Message.Contains("shorter than its interval"));
    }

    [Fact]
    public void UnknownAlertDimension_ListsTheCheckSOwn()
    {
        var (_, errors) = Validate(WithAlert("- dimension: Bandwidth", "  alert_value: \"1\""));

        errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Latency").And.Contain("Status");
    }

    [Fact]
    public void NonNumericValueOnAThresholdDimension_IsRejected()
    {
        // Stored as a string either way, so without this it only fails inside the evaluator, long
        // after the apply reported success.
        var (_, errors) = Validate(WithAlert("- dimension: Latency", "  alert_value: slow"));

        errors.Should().ContainSingle().Which.Message.Should().Contain("numerically");
    }

    [Fact]
    public void NonStatusValueOnAnEqualityDimension_IsRejected()
    {
        var (_, errors) = Validate(WithAlert("- dimension: Status", "  alert_value: broken"));

        errors.Should().ContainSingle().Which.Message.Should().Contain("not a valid status");
    }

    [Fact]
    public void TwoRulesOnTheSameDimension_AreRejected()
    {
        // Dimension is the rule's identity, so two rules sharing one would be unmatchable.
        var (_, errors) = Validate(WithAlert(
            "- dimension: Latency", "  alert_value: \"500\"",
            "- dimension: Latency", "  alert_value: \"2000\""));

        errors.Should().ContainSingle().Which.Message.Should().Contain("more than one alert config");
    }

    [Fact]
    public void ValidAlertConfig_ResolvesItsDimensionSpec()
    {
        var (services, errors) = Validate(WithAlert(
            "- dimension: Latency", "  alert_value: \"2000\"", "  severity: Critical"));

        errors.Should().BeEmpty();
        var alert = services[0].Checks[0].AlertConfigs.Should().ContainSingle().Subject;
        alert.Spec.Name.Should().Be("Latency");
        alert.Severity.Should().Be(Piro.Domain.Enums.AlertSeverity.Critical);
    }

    [Fact]
    public void InvalidSlug_IsRejected()
    {
        var (_, errors) = Validate("""
            version: 1
            services:
              - slug: Heva_API
                name: API
            """);

        errors.Should().ContainSingle().Which.Message.Should().Contain("not a valid slug");
    }

    [Fact]
    public void ServiceWithoutASlug_CannotBeReconciled()
    {
        var (services, errors) = Validate("""
            version: 1
            services:
              - name: API
            """);

        services.Should().BeEmpty();
        errors.Should().ContainSingle().Which.Message.Should().Contain("must declare a 'slug'");
    }

    /// <summary>
    /// Builds a document whose single check carries <paramref name="alertLines"/> as its alert_configs
    /// block. Lines are given unindented and indented here, so a test reads as the fields it is about.
    /// </summary>
    private static string WithAlert(params string[] alertLines)
    {
        var block = string.Join("\n", alertLines.Select(l => "          " + l));
        return $"""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "* * * * *"
                    type_data:
                      url: https://a.test/health
                    alert_configs:
            {block}
            """;
    }
}
