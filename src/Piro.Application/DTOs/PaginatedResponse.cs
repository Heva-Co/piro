namespace Piro.Application.DTOs;

/// <summary>Generic page of <typeparamref name="T"/> results plus the total matching count.</summary>
public record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
