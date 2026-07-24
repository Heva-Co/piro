namespace Piro.Domain.Tags;

/// <summary>
/// A single required worker tag: a key and an optional value. A null <see cref="Value"/> means "the worker
/// must carry this key with any value"; a non-null value means "the worker must carry this key with exactly
/// this value".
/// </summary>
public readonly record struct RequiredWorkerTag(string Key, string? Value);

/// <summary>
/// The flat, one-directional worker-tag match of RFC 0008 Part B (§4.5). A check names the worker tags it
/// accepts; a worker advertises its own tags; the check runs where they intersect. Pure and in-memory, so
/// it evaluates against the in-memory worker registry at dispatch time with no per-tick DB hit.
/// </summary>
public static class WorkerTagMatcher
{
    /// <summary>
    /// True if a worker carrying <paramref name="workerTags"/> is eligible for a check that declares
    /// <paramref name="required"/>. An empty requirement set matches every worker (today's behavior). A
    /// non-empty set matches when the worker shares at least one required (key, value) pair, where a
    /// null required value matches the key regardless of the worker's value.
    /// </summary>
    public static bool IsEligible(
        IReadOnlyCollection<RequiredWorkerTag> required,
        IReadOnlyDictionary<string, string?> workerTags)
    {
        if (required.Count == 0) return true; // no requirement ⇒ run anywhere

        foreach (var req in required)
        {
            if (!workerTags.TryGetValue(req.Key, out var workerValue)) continue;
            if (req.Value is null) return true;                              // key-present is enough
            if (string.Equals(req.Value, workerValue, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
