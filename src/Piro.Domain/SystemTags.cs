namespace Piro.Domain;

/// <summary>
/// How a system (<c>piro:*</c>) tag gets onto an entity (RFC 0008 §4.2). Piro always owns the key; this
/// axis says who owns the assignment.
/// </summary>
public enum SystemTagAssignment
{
    /// <summary>Piro derives the assignment from a fact on the entity and keeps it in sync; the user never touches it.</summary>
    Reconciled,

    /// <summary>The user associates/disassociates it on entities they choose; Piro has no fact to derive from.</summary>
    Assignable
}

/// <summary>
/// One entry in the curated <see cref="SystemTags"/> catalog: a Piro-owned key, how it is assigned, whether
/// it is materialized as a row (stored) or computed on read, and an optional closed value vocabulary.
/// </summary>
public record SystemTagDefinition(
    string Key,
    SystemTagAssignment Assignment,
    bool Stored,
    IReadOnlyList<string>? AllowedValues = null);

/// <summary>
/// The single source of truth for which <c>piro:*</c> keys exist and how each behaves. Declared in one
/// place rather than scattered as special-cases across services.
/// </summary>
public static class SystemTags
{
    public static readonly IReadOnlyList<SystemTagDefinition> All =
    [
        new("piro:check-type",   SystemTagAssignment.Reconciled, Stored: true),
        new("piro:region",       SystemTagAssignment.Reconciled, Stored: true),
        new("piro:builtin",      SystemTagAssignment.Reconciled, Stored: true),
        new("piro:default",      SystemTagAssignment.Reconciled, Stored: true),
        new("piro:3rd-party",    SystemTagAssignment.Assignable, Stored: true),  // key-only flag (AllowedValues null)
        new("piro:has-incident", SystemTagAssignment.Reconciled, Stored: false), // computed on read
        new("piro:has-alerts",   SystemTagAssignment.Reconciled, Stored: false),
    ];

    /// <summary>The catalog entry for a key, or null if the key is not a known system tag.</summary>
    public static SystemTagDefinition? Find(string key)
    {
        foreach (var d in All)
            if (d.Key == key) return d;
        return null;
    }
}
