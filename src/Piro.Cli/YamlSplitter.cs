namespace Piro.Cli;

/// <summary>
/// Splits one exported <c>piro.yaml</c> into a document per service, for <c>export --split</c>.
/// </summary>
/// <remarks>
/// Text-level rather than a parse-and-reserialize: the exported file is a known, machine-produced
/// shape, and slicing it preserves the comments the exporter deliberately emitted — notably the ones
/// marking checks that could not be represented, which a round-trip through a serializer would drop
/// (RFC 0019 §4.8). Keeping YamlDotNet out of the AOT binary is the other reason.
/// </remarks>
internal static class YamlSplitter
{
    private const string ServiceItemPrefix = "  - slug: ";

    public static IEnumerable<(string Slug, string Content)> SplitByService(string yaml)
    {
        var lines = yaml.ReplaceLineEndings("\n").Split('\n');

        // Everything before the first service — the schema comment and `version: 1` — is repeated
        // into every file, since each must stand alone and carry its own version (§4.6).
        var header = new List<string>();
        var index = 0;

        for (; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(ServiceItemPrefix, StringComparison.Ordinal)) break;
            if (lines[index].TrimStart().StartsWith("services:", StringComparison.Ordinal)) continue;
            header.Add(lines[index]);
        }

        string? slug = null;
        var body = new List<string>();

        for (; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.StartsWith(ServiceItemPrefix, StringComparison.Ordinal))
            {
                if (slug is not null) yield return (slug, Compose(header, body));
                slug = Unquote(line[ServiceItemPrefix.Length..].Trim());
                body = [line];
                continue;
            }

            if (slug is not null) body.Add(line);
        }

        if (slug is not null) yield return (slug, Compose(header, body));
    }

    private static string Compose(List<string> header, List<string> body)
    {
        var output = new List<string>(header.Where(l => l.Length > 0 || header.IndexOf(l) == 0));
        output.Add("services:");
        output.AddRange(body);

        return string.Join(Environment.NewLine, TrimTrailingBlanks(output)) + Environment.NewLine;
    }

    private static List<string> TrimTrailingBlanks(List<string> lines)
    {
        var end = lines.Count;
        while (end > 0 && string.IsNullOrWhiteSpace(lines[end - 1])) end--;
        return lines[..end];
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;
}
