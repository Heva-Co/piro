using FluentAssertions;
using Scriban;

namespace Piro.UnitTests;

/// <summary>
/// Pins the Scriban behaviour Piro's notification and email templates rely on. Added when Scriban
/// was moved off 7.1.0 to clear GHSA-24c8-4792-22hx and GHSA-7jvp-hj45-2f2m: the latter changes how
/// <c>TypedObjectAccessor</c> writes to CLR properties, so the rendering path deserved a test rather
/// than the assumption that a clean build meant a working upgrade.
/// </summary>
public class ScribanTemplateRenderingTests
{
    private static string Render(string template, object model)
    {
        var parsed = Template.Parse(template);
        parsed.HasErrors.Should().BeFalse(string.Join("; ", parsed.Messages));
        return parsed.Render(model);
    }

    [Fact]
    public void Substitutes_anonymous_model_properties()
    {
        // The shape EmailTemplates uses for invitations and password resets.
        var output = Render("Open {{ reset_url }} to continue.", new { reset_url = "https://piro.test/r/abc" });

        output.Should().Be("Open https://piro.test/r/abc to continue.");
    }

    [Fact]
    public void Renders_properties_of_a_typed_model()
    {
        // Alert emails pass a typed model, which is what TypedObjectAccessor handles — the accessor
        // the mass-assignment advisory tightened.
        var output = Render(
            "{{ service_name }} is {{ status }}",
            new AlertModel { ServiceName = "api-gateway", Status = "DOWN" });

        output.Should().Be("api-gateway is DOWN");
    }

    [Fact]
    public void Supports_conditionals_and_loops()
    {
        var template = "{{ if items.size > 0 }}{{ for i in items }}[{{ i }}]{{ end }}{{ else }}none{{ end }}";

        Render(template, new { items = new[] { "a", "b" } }).Should().Be("[a][b]");
        Render(template, new { items = Array.Empty<string>() }).Should().Be("none");
    }

    [Fact]
    public void Leaves_unknown_members_empty_rather_than_throwing()
    {
        // Templates are authored per integration; a typo must degrade the message, not break delivery.
        Render("value: {{ missing_member }}", new { present = 1 }).Should().Be("value: ");
    }

    [Fact]
    public void Template_writes_cannot_reach_model_properties()
    {
        // GHSA-7jvp-hj45-2f2m: a template assigning to a model member must not mutate the CLR object.
        var model = new AlertModel { ServiceName = "api-gateway", Status = "UP" };

        Render("{{ status = 'DOWN' }}{{ service_name }}", model);

        model.Status.Should().Be("UP", "a rendered template must not write back into the model");
    }

    private class AlertModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
