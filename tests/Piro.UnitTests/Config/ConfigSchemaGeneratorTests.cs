using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Piro.Application.Config;
using Piro.Checks;
using Piro.Checks.Abstractions;

namespace Piro.UnitTests.Config;

/// <summary>
/// Covers the generated JSON Schema (RFC 0019 §4.10). The point of generating it is that it cannot
/// disagree with what the server deserializes, so these assert it really is derived from the
/// registry rather than restating a hand-written shape.
/// </summary>
public class ConfigSchemaGeneratorTests
{
    private readonly ICheckRegistry _registry = Substitute.For<ICheckRegistry>();

    private JsonElement Generate(params ICheck[] checks)
    {
        _registry.All.Returns(checks);
        var json = new ConfigSchemaGenerator(_registry).Generate();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static JsonElement Nav(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path) current = current.GetProperty(segment);
        return current;
    }

    [Fact]
    public void IsAValidSchemaDocument()
    {
        var schema = Generate(new HttpCheck());

        schema.GetProperty("$schema").GetString().Should().Contain("json-schema.org");
        schema.GetProperty("type").GetString().Should().Be("object");
        Nav(schema, "properties", "version").GetProperty("const").GetInt32().Should().Be(1);
    }

    [Fact]
    public void ServiceAndCheckRequireOnlyTheirIdentity()
    {
        var schema = Generate(new HttpCheck());
        var service = Nav(schema, "properties", "services", "items");

        service.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo("slug", "name");

        var check = Nav(service, "properties", "checks", "items");
        check.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo("slug", "name", "type", "cron");
    }

    [Fact]
    public void UnknownFieldsAreRejected()
    {
        // Strictness is the reason the schema helps: a typo must be flagged in the editor, because
        // under patch semantics an unrecognised key silently means "not declared".
        var schema = Generate(new HttpCheck());
        var service = Nav(schema, "properties", "services", "items");

        service.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Nav(service, "properties", "checks", "items")
            .GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void TheTypeEnumComesFromTheRegistry()
    {
        var schema = Generate(new HttpCheck(), new DnsCheck());

        var types = Nav(schema, "properties", "services", "items", "properties", "checks", "items",
                "properties", "type")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString());

        types.Should().BeEquivalentTo("HTTP", "DNS");
    }

    [Fact]
    public void EachCheckTypeGetsItsOwnTypeDataShape()
    {
        // The core of the design: type_data's shape depends on the check's type, so the schema binds
        // them with one conditional per registered check.
        var schema = Generate(new HttpCheck(), new DnsCheck());
        var check = Nav(schema, "properties", "services", "items", "properties", "checks", "items");

        var branches = check.GetProperty("allOf").EnumerateArray().ToList();
        branches.Should().HaveCount(2);

        var http = branches.Single(b =>
            Nav(b, "if", "properties", "type").GetProperty("const").GetString() == "HTTP");
        var httpFields = Nav(http, "then", "properties", "type_data", "properties");

        httpFields.TryGetProperty("url", out _).Should().BeTrue();
        httpFields.TryGetProperty("hostname", out _).Should().BeFalse();

        var dns = branches.Single(b =>
            Nav(b, "if", "properties", "type").GetProperty("const").GetString() == "DNS");
        Nav(dns, "then", "properties", "type_data", "properties")
            .TryGetProperty("url", out _).Should().BeFalse();
    }

    [Fact]
    public void EachBranchRequiresTheTypeItMatches()
    {
        // Without `required: [type]` the branch also matches a check that omits `type`, and every
        // branch would apply at once — making any type_data invalid.
        var schema = Generate(new HttpCheck(), new DnsCheck());
        var check = Nav(schema, "properties", "services", "items", "properties", "checks", "items");

        foreach (var branch in check.GetProperty("allOf").EnumerateArray())
            branch.GetProperty("if").GetProperty("required").EnumerateArray()
                .Select(e => e.GetString()).Should().Contain("type");
    }

    [Fact]
    public void FieldTypesAndDefaultsAreReflected()
    {
        var schema = Generate(new HttpCheck());
        var fields = HttpTypeData(schema);

        fields.GetProperty("timeout").GetProperty("type").GetString().Should().Be("number");
        fields.GetProperty("timeout").GetProperty("default").GetInt32().Should().Be(5000);
        fields.GetProperty("followRedirects").GetProperty("type").GetString().Should().Be("boolean");
        fields.GetProperty("headers").GetProperty("type").GetString().Should().Be("object");
        fields.GetProperty("method").GetProperty("enum").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("GET");
    }

    [Fact]
    public void ANestedObjectListCarriesItsElementShape()
    {
        // An HTTP check's ResponseRules is a list of records; the editor needs the element shape or
        // it cannot complete anything inside the list.
        var rules = HttpTypeData(Generate(new HttpCheck())).GetProperty("responseRules");

        rules.GetProperty("type").GetString().Should().Be("array");
        Nav(rules, "items", "properties").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public void AConditionallyVisibleFieldIsNotGloballyRequired()
    {
        // An HTTP body applies only to POST/PUT/PATCH, so requiring it outright would reject a
        // perfectly valid GET check.
        var typeData = Nav(Generate(new HttpCheck()),
            "properties", "services", "items", "properties", "checks", "items", "allOf");

        var http = typeData.EnumerateArray().Single();
        var body = Nav(http, "then", "properties", "type_data");

        if (body.TryGetProperty("required", out var required))
            required.EnumerateArray().Select(e => e.GetString()).Should().NotContain("body");
    }

    [Fact]
    public void ACheckRequiringAnIntegrationIsExcluded()
    {
        // The validator rejects it in YAML (§2), so offering it here would autocomplete a file that
        // can never apply.
        var gcp = Substitute.For<ICheck>();
        gcp.CheckId.Returns("GCP_CloudRunJob");
        gcp.Manifest.Returns(new CheckManifest
        {
            Label = "GCP Cloud Run Job",
            Description = "Cloud Run job",
            ConfigType = typeof(object),
            RequiredIntegration = "GoogleCloud",
        });

        var schema = Generate(new HttpCheck(), gcp);

        var types = Nav(schema, "properties", "services", "items", "properties", "checks", "items",
                "properties", "type")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString());

        types.Should().BeEquivalentTo("HTTP");
    }

    [Fact]
    public void AlertDimensionsComeFromTheRegisteredChecks()
    {
        var schema = Generate(new HttpCheck());

        var dimensions = Nav(schema, "properties", "services", "items", "properties", "checks",
                "items", "properties", "alert_configs", "items", "properties", "dimension")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString());

        dimensions.Should().Contain(["Status", "Latency"]);
    }

    [Fact]
    public void ARenamedFieldAcceptsBothItsSpellings()
    {
        // An HTTP check's TimeoutMs is stored as "timeout" via [JsonPropertyName], but the server
        // binds case-insensitively so "timeoutMs" also loads — and config written before that
        // attribute existed uses it. Rejecting the alias made a real exported document fail its own
        // schema, so the schema must describe both.
        var fields = HttpTypeData(Generate(new HttpCheck()));

        fields.TryGetProperty("timeout", out var canonical).Should().BeTrue();
        fields.TryGetProperty("timeoutMs", out var alias).Should().BeTrue();

        canonical.GetProperty("type").GetString().Should().Be("number");
        alias.GetProperty("type").GetString().Should().Be("number");
    }

    [Fact]
    public void AFieldWithNoRenameHasNoAlias()
    {
        // Only a renamed property gets an alias; otherwise every field would gain a duplicate and
        // additionalProperties: false would stop catching typos.
        var fields = HttpTypeData(Generate(new HttpCheck()));
        var keys = fields.EnumerateObject().Select(p => p.Name).ToList();

        keys.Should().Contain("url");
        keys.Should().NotContain("urlUrl");
        keys.Count(k => k.StartsWith("followRedirects", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void SlugsAreConstrainedToTheFormatTheValidatorAccepts()
    {
        var schema = Generate(new HttpCheck());

        Nav(schema, "properties", "services", "items", "properties", "slug")
            .GetProperty("pattern").GetString().Should().Be("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }

    private static JsonElement HttpTypeData(JsonElement schema) =>
        Nav(Nav(schema, "properties", "services", "items", "properties", "checks", "items", "allOf")
                .EnumerateArray().Single(),
            "then", "properties", "type_data", "properties");
}
