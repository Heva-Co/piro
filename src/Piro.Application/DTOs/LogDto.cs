namespace Piro.Application.DTOs;

public record LogDto(
    long Id,
    DateTime Timestamp,
    string Level,
    string Message,
    string? Exception,
    string? Source,
    string? Properties
);

public record LogQueryParams(
    string? Level = null,
    string? Search = null,
    DateTime? From = null,
    DateTime? To = null,
    int? CheckId = null,
    int Page = 1,
    int PageSize = 50
);

public record LogPageDto(
    IEnumerable<LogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
