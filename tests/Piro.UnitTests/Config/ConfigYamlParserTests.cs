using FluentAssertions;
using Piro.Application.Config;
using Piro.Application.DTOs;

namespace Piro.UnitTests.Config;

/// <summary>
/// Covers the piro.yaml parser (RFC 0019 §4.1): the declared-versus-absent distinction that patch
/// semantics rest on, positional error reporting, and the fields the format refuses to accept.
/// </summary>
public class ConfigYamlParserTests
{
    private readonly List<ConfigValidationError> _errors = [];

    private ConfigDocument? Parse(string yaml) =>
        ConfigYamlParser.Parse(new ConfigDocumentSource("piro.yaml", yaml), _errors);

    [Fact]
    public void ParsesTheMinimumValidService()
    {
        var doc = Parse("""
            version: 1
            services:
              - slug: heva-api
                name: Heva API
            """);

        _errors.Should().BeEmpty();
        doc!.Version.Should().Be(1);
        var service = doc.Services.Should().ContainSingle().Subject;
        service.Slug.Should().Be("heva-api");
        service.Name.Should().Be("Heva API");

        // The whole design rests on this: an undeclared field is null, never a default that would
        // overwrite what the admin panel set (§3).
        service.Description.Should().BeNull();
        service.IsHidden.Should().BeNull();
        service.DisplayOrder.Should().BeNull();
        service.DefaultStatus.Should().BeNull();
    }

    [Fact]
    public void ParsesChecksAlertConfigsAndTypeData()
    {
        var doc = Parse("""
            version: 1
            services:
              - slug: heva-api
                name: Heva API
                default_status: UP
                checks:
                  - slug: health
                    name: Health
                    type: HTTP
                    cron: "* * * * *"
                    is_active: true
                    required_worker_tags:
                      piro:region: eu-west
                      tier: primary
                    type_data:
                      url: https://api.heva.com/health
                      expectedStatusCode: 200
                      timeout: 5000
                      followRedirects: false
                    alert_configs:
                      - dimension: Latency
                        alert_value: "2000"
                        severity: Critical
                        failure_threshold: 3
                        min_failing_regions: 2
            """);

        _errors.Should().BeEmpty();
        var service = doc!.Services.Should().ContainSingle().Subject;
        service.DefaultStatus.Should().Be("UP");

        var check = service.Checks.Should().ContainSingle().Subject;
        check.Type.Should().Be("HTTP");
        check.Cron.Should().Be("* * * * *");
        check.IsActive.Should().BeTrue();
        check.RequiredWorkerTags.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["piro:region"] = "eu-west",
            ["tier"] = "primary",
        });

        // type_data keeps YAML's scalar types, so the JSON it becomes has real numbers and booleans
        // rather than quoted strings the config type would fail to bind.
        check.TypeData!["url"].Should().Be("https://api.heva.com/health");
        check.TypeData["expectedStatusCode"].Should().Be(200L);
        check.TypeData["followRedirects"].Should().Be(false);

        var alert = check.AlertConfigs.Should().ContainSingle().Subject;
        alert.Dimension.Should().Be("Latency");
        alert.AlertValue.Should().Be("2000");
        alert.Severity.Should().Be("Critical");
        alert.FailureThreshold.Should().Be(3);
        alert.MinFailingRegions.Should().Be(2);
        alert.SuccessThreshold.Should().BeNull();
    }

    [Fact]
    public void RequiredWorkerTags_AlsoAcceptsABareListOfKeys()
    {
        // A key-only flag carries no value, and a list is the natural way to write that.
        var doc = Parse("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    required_worker_tags: [eu-west, primary]
            """);

        _errors.Should().BeEmpty();
        doc!.Services[0].Checks[0].RequiredWorkerTags.Should().BeEquivalentTo(
            new Dictionary<string, string?> { ["eu-west"] = null, ["primary"] = null });
    }

    [Fact]
    public void UnknownField_IsAnErrorNamingTheAlternatives()
    {
        // Under patch semantics a typo is dangerous precisely because it is silent: "crons" would
        // read as "cron not declared" and leave the real schedule untouched.
        Parse("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    crons: "* * * * *"
            """);

        var error = _errors.Should().ContainSingle().Subject;
        error.Message.Should().Contain("Unknown field 'crons'").And.Contain("cron");
        error.Path.Should().Be("piro.yaml");
        error.Line.Should().Be(8);
        error.Pointer.Should().Be("services[0].checks[0].crons");
    }

    [Fact]
    public void IntegrationReference_IsRejectedWithItsReason()
    {
        Parse("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: job
                    name: Job
                    integration: gcp-prod
            """);

        _errors.Should().ContainSingle()
            .Which.Message.Should().Contain("credentials");
    }

    [Fact]
    public void DerivedAndStatefulAlertFields_AreRejected()
    {
        Parse("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: health
                    name: Health
                    alert_configs:
                      - dimension: Latency
                        comparison: Threshold
                        is_alerting: true
            """);

        _errors.Should().HaveCount(2);
        _errors.Should().Contain(e => e.Message.Contains("dimension spec"));
        _errors.Should().Contain(e => e.Message.Contains("live alert state"));
    }

    [Fact]
    public void CollectsEveryError_RatherThanStoppingAtTheFirst()
    {
        // Ten round-trips to fix ten typos is a broken workflow (§4.3).
        Parse("""
            version: 1
            services:
              - slug: api
                name: API
                is_hidden: maybe
                display_order: soon
                checks:
                  - slug: health
                    name: Health
                    type_data: not-a-mapping
            """);

        _errors.Should().HaveCount(3);
        _errors.Select(e => e.Pointer).Should().BeEquivalentTo(
            "services[0].is_hidden",
            "services[0].display_order",
            "services[0].checks[0].type_data");
    }

    [Fact]
    public void MalformedYaml_ReportsPositionInsteadOfThrowing()
    {
        var doc = Parse("""
            version: 1
            services:
              - slug: api
               name: API
            """);

        doc.Should().BeNull();
        var error = _errors.Should().ContainSingle().Subject;
        error.Path.Should().Be("piro.yaml");
        error.Line.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EmptyFile_IsAnErrorNotAnEmptyDocument()
    {
        // Silently contributing nothing is what turns a --prune into unintended deletion (§4.6).
        Parse("   \n");

        _errors.Should().ContainSingle().Which.Message.Should().Contain("empty");
    }

    [Fact]
    public void QuotedScalarStaysAString()
    {
        var doc = Parse("""
            version: 1
            services:
              - slug: api
                name: API
                checks:
                  - slug: c
                    name: C
                    type_data:
                      code: "0080"
                      port: 8080
            """);

        _errors.Should().BeEmpty();
        var typeData = doc!.Services[0].Checks[0].TypeData!;
        typeData["code"].Should().Be("0080");
        typeData["port"].Should().Be(8080L);
    }
}
