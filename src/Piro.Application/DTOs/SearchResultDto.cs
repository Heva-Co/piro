namespace Piro.Application.DTOs;

/// <summary>One matched entity in a global search result set.</summary>
public record SearchResultDto(
    string Type,
    string Title,
    string? Subtitle,
    string Url,
    int? IncidentId,
    string? IncidentUrl
);

public record SearchResultsDto(List<SearchResultDto> Results);
