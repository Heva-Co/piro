using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>Cross-entity substring search for the admin panel's global search (Cmd+K).</summary>
public interface ISearchRepository
{
    /// <summary>
    /// Searches Service, Check, Alert, Incident, Maintenance, OnCallSchedule, and EscalationPolicy by name/title/slug.
    /// When <paramref name="includeUsersAndApiKeys"/> is true, also searches AppUser and ApiKey — callers must only
    /// pass true for Owner/Admin roles.
    /// </summary>
    Task<List<SearchResultDto>> SearchAsync(string query, bool includeUsersAndApiKeys, CancellationToken ct = default);
}
