namespace Piro.Domain.Enums;

/// <summary>
/// The shape of a postmortem analysis field, declared on its <see cref="Entities.PostmortemFieldDefinition"/>.
/// The eight standard sections are all <see cref="LongText"/>. The enum exists from Phase 1 so the
/// definition table can describe a field's input type; richer per-type validation and input widgets
/// beyond a textarea are a later phase (RFC 0005 §4.3, §6).
/// </summary>
public enum PostmortemFieldType
{
    Text,
    LongText,
    Date,
    Select
}
