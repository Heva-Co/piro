namespace Piro.Domain.Tags;

/// <summary>
/// How a <see cref="TagTerm"/> matches (RFC 0008 §4.2 selector grammar). A closed set, so a selector can
/// never carry an operator the evaluator does not understand and there is no expression to inject.
/// </summary>
public enum TagOp
{
    /// <summary>The key is present and its value equals the single value in <see cref="TagTerm.Values"/>.</summary>
    Equals,

    /// <summary>The key is present with a value in <see cref="TagTerm.Values"/>.</summary>
    In,

    /// <summary>The key is absent, or present with a value not in <see cref="TagTerm.Values"/>.</summary>
    NotIn,

    /// <summary>The key is present (with or without a value).</summary>
    Exists
}

/// <summary>One match term: an operator applied to a key and, for the valued operators, a set of values.</summary>
public sealed record TagTerm(string Key, TagOp Op, IReadOnlyList<string>? Values = null);

/// <summary>
/// A tag selector authored by an admin and stored as JSON (RFC 0008 §4.2). <see cref="AllOf"/> terms are
/// ANDed, <see cref="AnyOf"/> terms are ORed, and the two groups are ANDed together. An empty (or absent)
/// selector matches everything, following the Kubernetes convention. This record is itself the JSON schema:
/// it (de)serializes directly with System.Text.Json, so there is no separate parser.
/// </summary>
public sealed record TagSelector(
    IReadOnlyList<TagTerm>? AllOf = null,
    IReadOnlyList<TagTerm>? AnyOf = null);
