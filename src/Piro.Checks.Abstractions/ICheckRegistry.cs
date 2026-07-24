namespace Piro.Checks.Abstractions;

/// <summary>
/// The set of check types Piro was built with, discovered from the explicit compile-time registry
/// (RFC 0016-style) — the replacement for iterating the closed <c>CheckType</c> enum. Keyed by
/// <see cref="ICheck.CheckId"/>. Closed at build time (the checks the app references) yet open at the
/// type level (a string id, not an enum value).
/// </summary>
public interface ICheckRegistry
{
    /// <summary>All registered checks, in no particular order.</summary>
    IReadOnlyList<ICheck> All { get; }

    /// <summary>The check with this id, or null if none is registered (e.g. an unknown/legacy id).</summary>
    ICheck? Find(string checkId);
}
