using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Models;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Piro.Application.Services;

/// <summary>Parses a piro.yaml file and either previews (dry-run) or applies the changes.</summary>
public class YamlImportService(
    IServiceRepository serviceRepo,
    ICheckRepository checkRepo,
    IAlertConfigRepository alertConfigRepo,
    ICheckSchedulerService checkScheduler)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public Task<ImportPlanDto> PlanAsync(string yaml, CancellationToken ct = default) =>
        ExecuteAsync(yaml, dryRun: true, ct);

    public Task<ImportPlanDto> ApplyAsync(string yaml, CancellationToken ct = default) =>
        ExecuteAsync(yaml, dryRun: false, ct);

    private async Task<ImportPlanDto> ExecuteAsync(string yaml, bool dryRun, CancellationToken ct)
    {
        PiroYamlConfig config;
        try
        {
            config = Deserializer.Deserialize<PiroYamlConfig>(yaml)
                ?? new PiroYamlConfig();
        }
        catch (Exception ex)
        {
            return new ImportPlanDto([], [$"Failed to parse YAML: {ex.Message}"]);
        }

        var entries = new List<ImportPlanEntryDto>();
        var errors = new List<string>();

        // ── Services + Checks ────────────────────────────────────────────────
        foreach (var s in config.Services ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Slug))
            { errors.Add("Service entry missing required field 'slug'."); continue; }
            if (string.IsNullOrWhiteSpace(s.Name))
            { errors.Add($"Service '{s.Slug}' missing required field 'name'."); continue; }

            var existingService = await serviceRepo.GetBySlugAsync(s.Slug, ct);
            Service service;

            if (existingService is not null)
            {
                bool changed = existingService.Name != s.Name
                    || existingService.Description != s.Description;

                entries.Add(changed
                    ? Entry("Service", s.Name, s.Slug, null, "Update", null)
                    : Skip("Service", s.Name, s.Slug));

                if (!dryRun && changed)
                {
                    existingService.Name = s.Name;
                    existingService.Description = s.Description;
                    existingService.UpdatedAt = DateTime.UtcNow;
                    await serviceRepo.UpdateAsync(existingService, ct);
                }
                service = existingService;
            }
            else
            {
                entries.Add(Entry("Service", s.Name, s.Slug, null, "Create", null));
                if (!dryRun)
                {
                    service = await serviceRepo.CreateAsync(new Service
                    {
                        Slug = s.Slug,
                        Name = s.Name,
                        Description = s.Description,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    }, ct);
                }
                else
                {
                    // Placeholder for dry-run — checks under this new service are previewed with no real ID
                    service = new Service { Id = 0, Slug = s.Slug, Name = s.Name };
                }
            }

            // ── Checks within this service ────────────────────────────────
            foreach (var c in s.Checks ?? [])
            {
                if (string.IsNullOrWhiteSpace(c.Slug))
                { errors.Add($"Check in service '{s.Slug}' missing required field 'slug'."); continue; }
                if (!Enum.TryParse<CheckType>(c.Type, ignoreCase: true, out var checkType))
                { errors.Add($"Check '{c.Slug}' in service '{s.Slug}': unknown type '{c.Type}'."); continue; }

                var typeDataJson = ToJson(c.TypeData);
                var displayName = string.IsNullOrWhiteSpace(c.Name) ? c.Slug : c.Name;

                // For new services in dry-run there is no ID, so we can't query checks
                var existingCheck = service.Id > 0
                    ? await checkRepo.GetBySlugAsync(service.Id, c.Slug, ct)
                    : null;

                if (existingCheck is not null)
                {
                    bool changed = existingCheck.Name != displayName
                        || existingCheck.Cron != c.Cron
                        || existingCheck.TypeDataJson != typeDataJson
                        || existingCheck.Description != c.Description
                        || existingCheck.IsActive != c.IsActive
                        || existingCheck.IsMultiRegion != c.IsMultiRegion
                        || existingCheck.FailureThreshold != c.FailureThreshold
                        || existingCheck.RecoveryThreshold != c.RecoveryThreshold
                        || existingCheck.HistoryDaysDesktop != c.HistoryDaysDesktop
                        || existingCheck.HistoryDaysMobile != c.HistoryDaysMobile;

                    if (!changed) { entries.Add(Skip("Check", displayName, c.Slug, s.Slug)); continue; }

                    entries.Add(Entry("Check", displayName, c.Slug, s.Slug, "Update", $"type: {c.Type}"));
                    if (!dryRun)
                    {
                        existingCheck.Name = displayName;
                        existingCheck.Cron = c.Cron;
                        existingCheck.TypeDataJson = typeDataJson;
                        existingCheck.Description = c.Description;
                        existingCheck.IsActive = c.IsActive;
                        existingCheck.IsMultiRegion = c.IsMultiRegion;
                        existingCheck.FailureThreshold = c.FailureThreshold;
                        existingCheck.RecoveryThreshold = c.RecoveryThreshold;
                        existingCheck.HistoryDaysDesktop = c.HistoryDaysDesktop;
                        existingCheck.HistoryDaysMobile = c.HistoryDaysMobile;
                        existingCheck.UpdatedAt = DateTime.UtcNow;
                        await checkRepo.UpdateAsync(existingCheck, ct);
                        await checkScheduler.ScheduleAsync(existingCheck, ct);
                    }
                }
                else
                {
                    entries.Add(Entry("Check", displayName, c.Slug, s.Slug, "Create", $"type: {c.Type}"));
                    if (!dryRun && service.Id > 0)
                    {
                        var newCheck = await checkRepo.CreateAsync(new Check
                        {
                            ServiceId = service.Id,
                            Slug = c.Slug,
                            Name = displayName,
                            Type = checkType,
                            Cron = c.Cron,
                            TypeDataJson = typeDataJson,
                            Description = c.Description,
                            IsActive = c.IsActive,
                            IsMultiRegion = c.IsMultiRegion,
                            FailureThreshold = c.FailureThreshold,
                            RecoveryThreshold = c.RecoveryThreshold,
                            HistoryDaysDesktop = c.HistoryDaysDesktop,
                            HistoryDaysMobile = c.HistoryDaysMobile,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        }, ct);
                        await checkScheduler.ScheduleAsync(newCheck, ct);
                        await SyncAlertsAsync(newCheck.Id, c.Alerts ?? [], entries, s.Slug, c.Slug, dryRun, ct);
                    }
                    else if (!dryRun)
                    {
                        // dryRun preview for alerts under new check
                        await SyncAlertsAsync(0, c.Alerts ?? [], entries, s.Slug, c.Slug, dryRun: true, ct);
                    }
                }

                // Sync alerts for existing checks (outside dryRun guard so preview works)
                if (existingCheck is not null)
                    await SyncAlertsAsync(existingCheck.Id, c.Alerts ?? [], entries, s.Slug, c.Slug, dryRun, ct);
            }
        }


        return new ImportPlanDto(entries, errors);
    }

    // ── Alert sync ───────────────────────────────────────────────────────────

    private async Task SyncAlertsAsync(
        int checkId,
        List<AlertYamlEntry> yamlAlerts,
        List<ImportPlanEntryDto> entries,
        string serviceSlug, string checkSlug,
        bool dryRun, CancellationToken ct)
    {
        if (yamlAlerts.Count == 0) return;

        var existing = checkId > 0
            ? (await alertConfigRepo.GetByCheckIdAsync(checkId, ct)).ToList()
            : [];

        foreach (var a in yamlAlerts)
        {
            if (!Enum.TryParse<AlertFor>(a.AlertFor, ignoreCase: true, out var alertFor)) continue;
            if (!Enum.TryParse<AlertSeverity>(a.Severity, ignoreCase: true, out var severity)) continue;

            var match = existing.FirstOrDefault(e =>
                e.AlertFor == alertFor && e.AlertValue == a.AlertValue);

            if (match is not null)
            {
                bool changed = match.FailureThreshold != a.FailureThreshold
                    || match.SuccessThreshold != a.SuccessThreshold
                    || match.Severity != severity
                    || match.Description != a.Description;

                if (!changed) { entries.Add(Skip("Alert", $"{a.AlertFor}:{a.AlertValue}", checkSlug, serviceSlug)); continue; }

                entries.Add(Entry("Alert", $"{a.AlertFor}:{a.AlertValue}", checkSlug, serviceSlug, "Update", null));
                if (!dryRun)
                {
                    match.FailureThreshold = a.FailureThreshold;
                    match.SuccessThreshold = a.SuccessThreshold;
                    match.Severity = severity;
                    match.Description = a.Description;
                    match.UpdatedAt = DateTime.UtcNow;
                    await alertConfigRepo.UpdateAsync(match, ct);
                }
            }
            else
            {
                entries.Add(Entry("Alert", $"{a.AlertFor}:{a.AlertValue}", checkSlug, serviceSlug, "Create", null));
                if (!dryRun && checkId > 0)
                    await alertConfigRepo.CreateAsync(new AlertConfig
                    {
                        CheckId = checkId,
                        AlertFor = alertFor,
                        AlertValue = a.AlertValue,
                        FailureThreshold = a.FailureThreshold,
                        SuccessThreshold = a.SuccessThreshold,
                        Severity = severity,
                        Description = a.Description,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    }, ct);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ImportPlanEntryDto Entry(string type, string name, string? slug, string? parent, string action, string? details) =>
        new(type, name, slug, parent, action, details);

    private static ImportPlanEntryDto Skip(string type, string name, string? slug = null, string? parent = null) =>
        new(type, name, slug, parent, "Skip", "No changes");

    /// <summary>Converts a YamlDotNet generic node (Dictionary&lt;object,object&gt; / List&lt;object&gt; / scalar) to a JSON string.</summary>
    private static string ToJson(object? node)
    {
        if (node is null) return "{}";
        return JsonSerializer.Serialize(Normalize(node));
    }

    private static object? Normalize(object? node) => node switch
    {
        Dictionary<object, object> dict => dict.ToDictionary(
            kv => kv.Key?.ToString() ?? "",
            kv => Normalize(kv.Value)),
        List<object> list => list.Select(Normalize).ToList(),
        string s when bool.TryParse(s, out var b) => b,
        string s when long.TryParse(s, out var l) => l,
        string s when double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
        _ => node
    };
}
