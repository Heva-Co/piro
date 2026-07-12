using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Infrastructure.Persistence.Repositories;

public class SearchRepository(PiroDbContext db, UserManager<AppUser> userManager) : ISearchRepository
{
    private const int PerCategoryLimit = 5;

    public async Task<List<SearchResultDto>> SearchAsync(string query, bool includeUsersAndApiKeys, CancellationToken ct = default)
    {
        var pattern = $"%{query}%";
        var results = new List<SearchResultDto>();

        var services = await db.Services
            .Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.Slug, pattern))
            .OrderBy(s => s.Name)
            .Take(PerCategoryLimit)
            .Select(s => new SearchResultDto("Service", s.Name, s.Slug, $"/admin/services/{s.Slug}", null, null))
            .ToListAsync(ct);
        results.AddRange(services);

        var checks = await db.Checks
            .Include(c => c.Service)
            .Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Slug, pattern))
            .OrderBy(c => c.Name)
            .Take(PerCategoryLimit)
            .Select(c => new SearchResultDto("Check", c.Name, c.Service.Name, $"/admin/services/{c.Service.Slug}/checks/{c.Slug}", null, null))
            .ToListAsync(ct);
        results.AddRange(checks);

        var alerts = await db.Alerts
            .Include(a => a.Service)
            .Where(a => a.Message != null && EF.Functions.ILike(a.Message, pattern))
            .OrderByDescending(a => a.FiredAt)
            .Take(PerCategoryLimit)
            .Select(a => new SearchResultDto(
                "Alert",
                a.Message ?? $"Alert #{a.Id}",
                a.Service.Name,
                $"/admin/alerts/{a.Id}",
                a.IncidentId,
                a.IncidentId != null ? $"/admin/incidents/{a.IncidentId}" : null))
            .ToListAsync(ct);
        results.AddRange(alerts);

        var incidents = await db.Incidents
            .Where(i => EF.Functions.ILike(i.Title, pattern))
            .OrderByDescending(i => i.StartDateTime)
            .Take(PerCategoryLimit)
            .Select(i => new SearchResultDto("Incident", i.Title, null, $"/admin/incidents/{i.Id}", null, null))
            .ToListAsync(ct);
        results.AddRange(incidents);

        var maintenances = await db.Maintenances
            .Where(m => EF.Functions.ILike(m.Title, pattern))
            .OrderByDescending(m => m.StartDateTime)
            .Take(PerCategoryLimit)
            .Select(m => new SearchResultDto("Maintenance", m.Title, null, $"/admin/maintenances/{m.Id}", null, null))
            .ToListAsync(ct);
        results.AddRange(maintenances);

        var schedules = await db.OnCallSchedules
            .Where(s => EF.Functions.ILike(s.Name, pattern))
            .OrderBy(s => s.Name)
            .Take(PerCategoryLimit)
            .Select(s => new SearchResultDto("OnCallSchedule", s.Name, null, $"/admin/oncall/{s.Id}", null, null))
            .ToListAsync(ct);
        results.AddRange(schedules);

        var policies = await db.EscalationPolicies
            .Where(p => EF.Functions.ILike(p.Name, pattern))
            .OrderBy(p => p.Name)
            .Take(PerCategoryLimit)
            .Select(p => new SearchResultDto("EscalationPolicy", p.Name, null, $"/admin/escalation-policies/{p.Id}", null, null))
            .ToListAsync(ct);
        results.AddRange(policies);

        if (includeUsersAndApiKeys)
        {
            var users = await userManager.Users
                .Where(u => EF.Functions.ILike(u.Name, pattern) || (u.Email != null && EF.Functions.ILike(u.Email, pattern)))
                .OrderBy(u => u.Name)
                .Take(PerCategoryLimit)
                .Select(u => new SearchResultDto("User", u.Name, u.Email, $"/admin/configuration/users/{u.Id}", null, null))
                .ToListAsync(ct);
            results.AddRange(users);

            var apiKeys = await db.ApiKeys
                .Where(k => EF.Functions.ILike(k.Name, pattern))
                .OrderBy(k => k.Name)
                .Take(PerCategoryLimit)
                .Select(k => new SearchResultDto("ApiKey", k.Name, k.MaskedKey, "/admin/configuration/api-keys", null, null))
                .ToListAsync(ct);
            results.AddRange(apiKeys);
        }

        return results;
    }
}
