using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for <see cref="AlertConfig"/> CRUD within a check.</summary>
public class AlertConfigAppService(
    IAlertConfigRepository alertConfigRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository)
{
    public async Task<IEnumerable<AlertConfigDto>> GetByCheckAsync(
        string serviceSlug, string checkSlug, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var configs = await alertConfigRepository.GetByCheckIdAsync(check.Id, ct);
        return configs.Select(c => c.ToDto());
    }

    public async Task<AlertConfigDto> GetByIdAsync(
        string serviceSlug, string checkSlug, int id, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var config = await alertConfigRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(AlertConfig), id.ToString());
        if (config.CheckId != check.Id) throw new NotFoundException(nameof(AlertConfig), id.ToString());
        return config.ToDto();
    }

    public async Task<AlertConfigDto> CreateAsync(
        string serviceSlug, string checkSlug, CreateAlertConfigRequest request, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);

        // Restricted to one AlertConfig per Check for now — multi-config evaluation
        // (combining/prioritizing several rules on the same check) is deferred.
        var existing = await alertConfigRepository.GetByCheckIdAsync(check.Id, ct);
        if (existing.Any())
            throw new DomainValidationException("This check already has an alert configuration. Update it instead of creating a new one.");

        var config = new AlertConfig
        {
            CheckId = check.Id,
            AlertFor = request.AlertFor,
            AlertValue = request.AlertValue,
            FailureThreshold = request.FailureThreshold,
            SuccessThreshold = request.SuccessThreshold,
            Description = request.Description,
            CreateIncident = request.CreateIncident,
            IncidentThresholdOccurrences = request.IncidentThresholdOccurrences,
            IsActive = request.IsActive,
            Severity = request.Severity
        };

        var created = await alertConfigRepository.CreateAsync(config, ct);
        return created.ToDto();
    }

    public async Task<AlertConfigDto> UpdateAsync(
        string serviceSlug, string checkSlug, int id, UpdateAlertConfigRequest request, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var config = await alertConfigRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(AlertConfig), id.ToString());
        if (config.CheckId != check.Id) throw new NotFoundException(nameof(AlertConfig), id.ToString());

        if (request.AlertFor is not null) config.AlertFor = request.AlertFor.Value;
        if (request.AlertValue is not null) config.AlertValue = request.AlertValue;
        if (request.FailureThreshold is not null) config.FailureThreshold = request.FailureThreshold.Value;
        if (request.SuccessThreshold is not null) config.SuccessThreshold = request.SuccessThreshold.Value;
        if (request.Description is not null) config.Description = request.Description;
        if (request.CreateIncident is not null) config.CreateIncident = request.CreateIncident.Value;
        if (request.IncidentThresholdOccurrences is not null) config.IncidentThresholdOccurrences = request.IncidentThresholdOccurrences.Value;
        if (request.IsActive is not null) config.IsActive = request.IsActive.Value;
        if (request.Severity is not null) config.Severity = request.Severity.Value;

        var updated = await alertConfigRepository.UpdateAsync(config, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(
        string serviceSlug, string checkSlug, int id, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var config = await alertConfigRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(AlertConfig), id.ToString());
        if (config.CheckId != check.Id) throw new NotFoundException(nameof(AlertConfig), id.ToString());
        await alertConfigRepository.DeleteAsync(config, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Check> ResolveCheckAsync(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        return await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);
    }
}
