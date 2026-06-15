namespace Piro.Application.DTOs;

/// <summary>Result of a dry-run or applied YAML import.</summary>
public record ImportPlanDto(
    List<ImportPlanEntryDto> Entries,
    List<string> Errors
)
{
    public bool HasErrors => Errors.Count > 0;
    public int Created => Entries.Count(e => e.Action == "Create");
    public int Updated => Entries.Count(e => e.Action == "Update");
    public int Skipped => Entries.Count(e => e.Action == "Skip");
}

public record ImportPlanEntryDto(
    string EntityType,   // Trigger | Service | Check
    string Name,
    string? Slug,
    string? ParentSlug,  // service slug for checks
    string Action,       // Create | Update | Skip
    string? Details
);
