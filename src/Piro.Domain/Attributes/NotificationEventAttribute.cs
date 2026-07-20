namespace Piro.Domain.Attributes;

/// <summary>
/// Declares the stable catalog identity of a <see cref="Enums.NotificationEventType"/> value: its
/// wire name (the string that appears in subscriptions, webhook payloads, and cross-references) and a
/// human-readable description of when it fires. The catalog is closed and code-owned (RFC 0009 §4.2);
/// this attribute is what makes it self-documenting and reflectable — the subscription UI reads the
/// name and description straight off the enum rather than hardcoding a parallel list.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotificationEventAttribute(string name, string description) : Attribute
{
    /// <summary>
    /// The stable wire name, in <c>domain:...:verb</c> form (e.g. <c>alert:created</c>,
    /// <c>system:integration:expired</c>). Hierarchical with variable depth; the last segment is the
    /// state/verb. This is the identifier referenced from subscriptions and payloads — never renamed.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>Human-readable explanation of the exact condition that fires this event.</summary>
    public string Description { get; } = description;
}
