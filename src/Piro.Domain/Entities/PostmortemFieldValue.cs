namespace Piro.Domain.Entities;

/// <summary>
/// The content a given <see cref="Postmortem"/> wrote into one analysis section (RFC 0005 §4.3).
/// One row per report per active <see cref="PostmortemFieldDefinition"/>, inserted empty on report
/// creation and filled in by the author.
/// </summary>
public class PostmortemFieldValue
{
    public int Id { get; set; }

    /// <summary>FK to the parent <see cref="Postmortem"/>, cascade delete.</summary>
    public int PostmortemId { get; set; }

    /// <summary>FK to the <see cref="PostmortemFieldDefinition"/>, RESTRICT — a definition in use can't be hard-deleted (deactivate via IsActive instead).</summary>
    public int FieldDefinitionId { get; set; }

    /// <summary>The authored content. Plain text in Phase 1 (no Markdown), defaults to empty.</summary>
    public string Value { get; set; } = string.Empty;

    public Postmortem Postmortem { get; set; } = null!;
    public PostmortemFieldDefinition FieldDefinition { get; set; } = null!;
}
