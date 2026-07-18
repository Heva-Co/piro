using FluentAssertions;
using Piro.Application.Extensions;
using Piro.Domain.Checks.Config;
using Piro.Domain.Enums;

namespace Piro.UnitTests.Checks;

/// <summary>
/// Verifies ConfigSchemaBuilder infers the extended ConfigFieldType values (RFC 0011) from the
/// real *CheckConfig records' CLR types — Number/Boolean/StringList/KeyValue/ObjectArray — so the
/// config form can be rendered generically without a hand-written schema per type.
/// </summary>
public class ConfigSchemaBuilderTests
{
    [Fact]
    public void HttpCheckConfig_InfersScalarAndCompositeFieldTypes()
    {
        var schema = ConfigSchemaBuilder.For(typeof(HttpCheckConfig));

        // camelCase keys, matching the JSON naming policy used for ConfigJson.
        schema.Single(f => f.Key == "url").Type.Should().Be(ConfigFieldType.Url);  // [Url] annotation
        schema.Single(f => f.Key == "timeoutMs").Type.Should().Be(ConfigFieldType.Number);
        schema.Single(f => f.Key == "followRedirects").Type.Should().Be(ConfigFieldType.Boolean);
        schema.Single(f => f.Key == "headers").Type.Should().Be(ConfigFieldType.KeyValue);
        schema.Single(f => f.Key == "expectedStatusCodes").Type.Should().Be(ConfigFieldType.StringList);
    }

    [Fact]
    public void HttpCheckConfig_ResponseRules_IsObjectArrayWithNestedItemSchema()
    {
        var schema = ConfigSchemaBuilder.For(typeof(HttpCheckConfig));

        var rules = schema.Single(f => f.Key == "responseRules");
        rules.Type.Should().Be(ConfigFieldType.ObjectArray);
        rules.ItemSchema.Should().NotBeNull();

        // The nested schema is HttpResponseRule's own fields, reflected recursively.
        rules.ItemSchema!.Select(f => f.Key)
            .Should().Contain(["type", "value", "expected", "degraded"]);
        rules.ItemSchema!.Single(f => f.Key == "degraded").Type.Should().Be(ConfigFieldType.Boolean);
    }

    [Fact]
    public void TcpCheckConfig_PortAndTimeoutAreNumbers()
    {
        var schema = ConfigSchemaBuilder.For(typeof(TcpCheckConfig));

        schema.Single(f => f.Key == "port").Type.Should().Be(ConfigFieldType.Number);
        schema.Single(f => f.Key == "timeoutMs").Type.Should().Be(ConfigFieldType.Number);
    }

    [Fact]
    public void DnsCheckConfig_NameServersIsStringList()
    {
        var schema = ConfigSchemaBuilder.For(typeof(DnsCheckConfig));

        schema.Single(f => f.Key == "nameServers").Type.Should().Be(ConfigFieldType.StringList);
        schema.Single(f => f.Key == "host").Type.Should().Be(ConfigFieldType.String);
    }

    [Fact]
    public void ScalarFields_HaveNoItemSchema()
    {
        var schema = ConfigSchemaBuilder.For(typeof(TcpCheckConfig));

        schema.Should().OnlyContain(f => f.ItemSchema == null);
    }

    [Fact]
    public void Annotations_ProduceLabelsOptionsAndRequired()
    {
        var schema = ConfigSchemaBuilder.For(typeof(HttpCheckConfig));

        var url = schema.Single(f => f.Key == "url");
        url.Label.Should().Be("URL");
        url.Required.Should().BeTrue();
        url.Type.Should().Be(ConfigFieldType.Url);
        url.HelpText.Should().NotBeNullOrEmpty();

        var method = schema.Single(f => f.Key == "method");
        method.Type.Should().Be(ConfigFieldType.Enum);
        method.Options.Should().Contain(["GET", "POST", "HEAD"]);
    }

    [Fact]
    public void Body_IsVisibleOnlyForVerbsWithABody()
    {
        var body = ConfigSchemaBuilder.For(typeof(HttpCheckConfig)).Single(f => f.Key == "body");

        body.VisibleWhen.Should().NotBeNull();
        body.VisibleWhen!.Field.Should().Be("method");
        body.VisibleWhen.Values.Should().BeEquivalentTo(["POST", "PUT", "PATCH"]);
    }

    [Fact]
    public void UnconditionalField_HasNoVisibilityRule()
    {
        var url = ConfigSchemaBuilder.For(typeof(HttpCheckConfig)).Single(f => f.Key == "url");
        url.VisibleWhen.Should().BeNull();
    }

    [Fact]
    public void Defaults_AreReflectedFromTheRecordInitializers()
    {
        var http = ConfigSchemaBuilder.For(typeof(HttpCheckConfig));
        http.Single(f => f.Key == "method").Default.Should().Be("GET");
        http.Single(f => f.Key == "timeoutMs").Default.Should().Be(5000);
        http.Single(f => f.Key == "followRedirects").Default.Should().Be(true);

        ConfigSchemaBuilder.For(typeof(SslCheckConfig)).Single(f => f.Key == "port").Default.Should().Be(443);
    }
}
