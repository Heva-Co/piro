namespace Piro.Domain.Tags;

/// <summary>
/// Validates a <see cref="TagSelector"/> before it is stored (RFC 0008 §4.2), and parses the flat
/// <c>key:value</c> tag strings carried on event payloads into the dictionary the evaluator matches against.
/// </summary>
public static class TagSelectorValidation
{
    /// <summary>Returns null if the selector is well-formed, otherwise a human-readable rejection reason.</summary>
    public static string? Validate(TagSelector selector)
    {
        foreach (var term in (selector.AllOf ?? []).Concat(selector.AnyOf ?? []))
        {
            if (string.IsNullOrWhiteSpace(term.Key))
                return "A selector term must name a key.";

            var needsValues = term.Op is TagOp.Equals or TagOp.In or TagOp.NotIn;
            if (needsValues && (term.Values is null || term.Values.Count == 0))
                return $"Operator '{term.Op}' on key '{term.Key}' requires at least one value.";
            if (term.Op is TagOp.Equals && term.Values!.Count != 1)
                return $"Operator 'Equals' on key '{term.Key}' takes exactly one value.";
        }
        return null;
    }

    /// <summary>
    /// Parses flat tag strings (<c>"env:production"</c>, or key-only <c>"critical"</c>) into a
    /// key -&gt; value map, the shape <see cref="TagSelectorEvaluator.Matches"/> expects. A key with no colon
    /// maps to a null value; the first colon splits key from value, so a value may itself contain colons.
    /// On a duplicate key the last one wins (a key is unique per entity anyway).
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ParseTags(IEnumerable<string> tags)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var raw in tags)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var colon = raw.IndexOf(':');
            if (colon < 0)
                map[raw] = null;
            else
                map[raw[..colon]] = raw[(colon + 1)..];
        }
        return map;
    }
}
