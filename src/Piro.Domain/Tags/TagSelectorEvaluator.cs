namespace Piro.Domain.Tags;

/// <summary>
/// Evaluates a <see cref="TagSelector"/> against a tag set, in memory (RFC 0008 §4.2). Pure, no
/// dependencies, no regex, no expression compilation, so a semi-trusted admin-authored selector cannot
/// inject code or cause ReDoS. Used to filter notification events by a service's effective tags (#203).
/// </summary>
public static class TagSelectorEvaluator
{
    /// <summary>
    /// True if the selector matches the tag set. An empty/absent selector matches everything. A tag value
    /// is null for a key-only tag (e.g. <c>critical</c>).
    /// </summary>
    public static bool Matches(TagSelector selector, IReadOnlyDictionary<string, string?> tags)
    {
        var allOk = selector.AllOf is null || selector.AllOf.All(t => Match(t, tags));
        var anyOk = selector.AnyOf is null || selector.AnyOf.Count == 0 || selector.AnyOf.Any(t => Match(t, tags));
        return allOk && anyOk;
    }

    private static bool Match(TagTerm term, IReadOnlyDictionary<string, string?> tags)
    {
        return term.Op switch
        {
            // present (with or without a value)
            TagOp.Exists => tags.ContainsKey(term.Key),

            // present and its value equals the single expected value; a key-only tag (null value) never
            // Equals a concrete value
            TagOp.Equals => tags.TryGetValue(term.Key, out var v)
                            && v is not null
                            && string.Equals(v, term.Values?.FirstOrDefault(), StringComparison.Ordinal),

            // present with a value in the set
            TagOp.In => tags.TryGetValue(term.Key, out var v)
                        && v is not null
                        && (term.Values?.Contains(v, StringComparer.Ordinal) ?? false),

            // absent, or present with a value not in the set (k8s DoesNotExist intuition: a missing key
            // satisfies NotIn)
            TagOp.NotIn => !tags.TryGetValue(term.Key, out var v)
                           || v is null
                           || !(term.Values?.Contains(v, StringComparer.Ordinal) ?? false),

            _ => false
        };
    }
}
