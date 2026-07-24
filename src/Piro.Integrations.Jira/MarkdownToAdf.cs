using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Piro.Integrations.Jira;

/// <summary>
/// Converts the small Markdown subset Piro's own ticket drafts use into Atlassian Document Format (ADF),
/// the JSON body Jira Cloud REST v3 requires (it rejects raw Markdown) — RFC 0012 §4.6. Supports headings,
/// bullet lists, bold/italic, inline code, and links; anything unrecognized degrades to a plain paragraph.
/// This never throws — a conversion failure must not fail the ticket create.
/// </summary>
internal static partial class MarkdownToAdf
{
    public static JsonObject Convert(string markdown)
    {
        var content = new JsonArray();

        if (!string.IsNullOrWhiteSpace(markdown))
        {
            foreach (var block in SplitBlocks(markdown))
            {
                var node = ConvertBlock(block);
                if (node is not null)
                    content.Add(node);
            }
        }

        if (content.Count == 0)
            content.Add(Paragraph(""));

        return new JsonObject
        {
            ["type"] = "doc",
            ["version"] = 1,
            ["content"] = content,
        };
    }

    private static IEnumerable<string> SplitBlocks(string markdown) =>
        markdown.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

    private static JsonNode? ConvertBlock(string block)
    {
        var trimmed = block.Trim();
        if (trimmed.Length == 0) return null;

        // Heading: #, ##, ###
        var heading = HeadingRegex().Match(trimmed);
        if (heading.Success)
        {
            var level = Math.Min(heading.Groups[1].Value.Length, 6);
            return new JsonObject
            {
                ["type"] = "heading",
                ["attrs"] = new JsonObject { ["level"] = level },
                ["content"] = InlineContent(heading.Groups[2].Value),
            };
        }

        // Bullet list: every line starts with - or *
        var lines = trimmed.Split('\n');
        if (lines.All(l => BulletRegex().IsMatch(l.Trim())))
        {
            var items = new JsonArray();
            foreach (var line in lines)
            {
                var text = BulletRegex().Replace(line.Trim(), "");
                items.Add(new JsonObject
                {
                    ["type"] = "listItem",
                    ["content"] = new JsonArray { ParagraphWithInline(text) },
                });
            }
            return new JsonObject { ["type"] = "bulletList", ["content"] = items };
        }

        // Default: a paragraph (newlines inside collapse to spaces).
        return ParagraphWithInline(trimmed.Replace("\n", " "));
    }

    private static JsonObject ParagraphWithInline(string text) =>
        new() { ["type"] = "paragraph", ["content"] = InlineContent(text) };

    private static JsonObject Paragraph(string text) =>
        new()
        {
            ["type"] = "paragraph",
            ["content"] = new JsonArray { TextNode(text, null) },
        };

    /// <summary>Parses inline marks (bold, italic, code, links) into ADF text nodes.</summary>
    private static JsonArray InlineContent(string text)
    {
        var nodes = new JsonArray();
        foreach (Match m in InlineRegex().Matches(text))
        {
            if (m.Groups["link"].Success)
                nodes.Add(TextNode(m.Groups["ltext"].Value, LinkMarks(m.Groups["lhref"].Value)));
            else if (m.Groups["bold"].Success)
                nodes.Add(TextNode(m.Groups["btext"].Value, MarkArray("strong")));
            else if (m.Groups["italic"].Success)
                nodes.Add(TextNode(m.Groups["itext"].Value, MarkArray("em")));
            else if (m.Groups["code"].Success)
                nodes.Add(TextNode(m.Groups["ctext"].Value, MarkArray("code")));
            else if (m.Groups["plain"].Success && m.Groups["plain"].Value.Length > 0)
                nodes.Add(TextNode(m.Groups["plain"].Value, null));
        }
        if (nodes.Count == 0)
            nodes.Add(TextNode(text, null));
        return nodes;
    }

    private static JsonObject TextNode(string text, JsonArray? marks)
    {
        var node = new JsonObject { ["type"] = "text", ["text"] = text };
        if (marks is not null)
            node["marks"] = marks;
        return node;
    }

    private static JsonArray MarkArray(string type) => new() { new JsonObject { ["type"] = type } };

    private static JsonArray LinkMarks(string href) => new()
    {
        new JsonObject { ["type"] = "link", ["attrs"] = new JsonObject { ["href"] = href } },
    };

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[-*]\s+")]
    private static partial Regex BulletRegex();

    // One alternation per inline construct; the trailing 'plain' captures runs of ordinary text.
    [GeneratedRegex(
        @"(?<link>\[(?<ltext>[^\]]+)\]\((?<lhref>[^)]+)\))" +
        @"|(?<bold>\*\*(?<btext>[^*]+)\*\*)" +
        @"|(?<italic>\*(?<itext>[^*]+)\*)" +
        @"|(?<code>`(?<ctext>[^`]+)`)" +
        @"|(?<plain>(?:[^*`\[]|\[(?![^\]]+\]\())+)")]
    private static partial Regex InlineRegex();
}
