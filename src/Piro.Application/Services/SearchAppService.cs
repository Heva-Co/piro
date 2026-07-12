using Piro.Application.DTOs;
using Piro.Application.Interfaces;

namespace Piro.Application.Services;

/// <summary>Global cross-entity search for the admin panel (Cmd+K).</summary>
public class SearchAppService(ISearchRepository searchRepo)
{
    private const int MinQueryLength = 2;

    public async Task<List<SearchResultDto>> SearchAsync(string query, bool includeUsersAndApiKeys, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinQueryLength)
            return [];

        return await searchRepo.SearchAsync(query.Trim(), includeUsersAndApiKeys, ct);
    }
}
