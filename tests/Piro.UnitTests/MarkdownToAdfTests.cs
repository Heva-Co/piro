using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Piro.UnitTests;

/// <summary>
/// Verifies the Markdown→ADF conversion Jira issue bodies use (RFC 0012 §4.6): the small subset Piro's
/// drafts generate maps to valid ADF, and unrecognized input degrades to a paragraph rather than throwing.
/// MarkdownToAdf is internal to Piro.Integrations.Jira, so these drive it via reflection.
/// </summary>
public class MarkdownToAdfTests
{
    private static JsonNode Convert(string markdown)
    {
        var type = System.Reflection.Assembly.Load("Piro.Integrations.Jira")
            .GetType("Piro.Integrations.Jira.MarkdownToAdf")!;
        var method = type.GetMethod("Convert", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var result = method.Invoke(null, [markdown])!;
        // Re-parse through JsonNode so tests assert on a stable shape regardless of the concrete node type.
        return JsonNode.Parse(((JsonObject)result).ToJsonString())!;
    }

    [Fact]
    public void RootIsAdfDoc()
    {
        var doc = Convert("Hello world");

        doc["type"]!.GetValue<string>().Should().Be("doc");
        doc["version"]!.GetValue<int>().Should().Be(1);
        doc["content"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public void PlainParagraphBecomesParagraphNode()
    {
        var doc = Convert("Just a line.");
        var block = doc["content"]!.AsArray()[0]!;

        block["type"]!.GetValue<string>().Should().Be("paragraph");
        block["content"]!.AsArray()[0]!["text"]!.GetValue<string>().Should().Be("Just a line.");
    }

    [Fact]
    public void BoldAndLinkGetMarks()
    {
        var doc = Convert("A **bold** word and a [link](https://x.test).");
        var inline = doc["content"]!.AsArray()[0]!["content"]!.AsArray();

        inline.Should().Contain(n =>
            n!["text"]!.GetValue<string>() == "bold" &&
            n["marks"]!.AsArray()[0]!["type"]!.GetValue<string>() == "strong");

        var link = inline.Single(n => n!["text"]!.GetValue<string>() == "link");
        link!["marks"]!.AsArray()[0]!["type"]!.GetValue<string>().Should().Be("link");
        link["marks"]!.AsArray()[0]!["attrs"]!["href"]!.GetValue<string>().Should().Be("https://x.test");
    }

    [Fact]
    public void BulletListBecomesBulletList()
    {
        var doc = Convert("- one\n- two");
        var block = doc["content"]!.AsArray()[0]!;

        block["type"]!.GetValue<string>().Should().Be("bulletList");
        block["content"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public void EmptyInputStillProducesValidDoc()
    {
        var doc = Convert("");

        doc["type"]!.GetValue<string>().Should().Be("doc");
        doc["content"]!.AsArray().Should().ContainSingle()
            .Which!["type"]!.GetValue<string>().Should().Be("paragraph");
    }
}
