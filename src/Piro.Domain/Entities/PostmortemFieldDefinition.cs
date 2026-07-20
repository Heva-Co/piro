using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>
/// The template describing <em>which</em> analysis sections a postmortem has (RFC 0005 §4.3). Seeded
/// with the eight standard sections (Overview … Action Items) and not user-editable in Phase 1 — but
/// living in a table rather than an enum or columns means custom fields become possible later with no
/// schema change. Separated from the per-report <see cref="PostmortemFieldValue"/> content.
/// </summary>
public class PostmortemFieldDefinition
{
    public int Id { get; set; }

    /// <summary>Stable identifier, unique (e.g. <c>overview</c>, <c>root_causes</c>) — survives heading renames.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display heading (e.g. "Root Causes").</summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>The guidance blurb shown under each heading.</summary>
    public string? HelpText { get; set; }

    /// <summary>The input shape of the field. The eight standard sections are all <see cref="PostmortemFieldType.LongText"/>.</summary>
    public PostmortemFieldType FieldType { get; set; } = PostmortemFieldType.LongText;

    /// <summary>Display order; the standard sections are seeded 0–7.</summary>
    public int SortOrder { get; set; }

    /// <summary>Soft-disable a field without deleting historical values. Default true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>True for the eight seeded fields — marks them non-deletable and reserves the door for user-defined (false) fields later.</summary>
    public bool IsSystem { get; set; }
}
