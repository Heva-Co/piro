using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Checks.Abstractions;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

namespace Piro.Application.Services;

/// <summary>Application service for <see cref="AlertConfig"/> CRUD within a check.</summary>
public class AlertConfigAppService(
    IAlertConfigRepository alertConfigRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    ICheckRegistry checkRegistry)
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
        var spec = ResolveDimensionSpec(check, request.Dimension);

        var config = new AlertConfig
        {
            CheckId = check.Id,
            Dimension = spec.Name,
            Comparison = spec.Comparison,
            Direction = spec.Direction,
            AlertValue = request.AlertValue,
            FailureThreshold = request.FailureThreshold,
            SuccessThreshold = request.SuccessThreshold,
            Description = request.Description,
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

        if (request.Dimension is not null)
        {
            var spec = ResolveDimensionSpec(check, request.Dimension);
            config.Dimension = spec.Name;
            config.Comparison = spec.Comparison;
            config.Direction = spec.Direction;
        }
        if (request.AlertValue is not null) config.AlertValue = request.AlertValue;
        if (request.FailureThreshold is not null) config.FailureThreshold = request.FailureThreshold.Value;
        if (request.SuccessThreshold is not null) config.SuccessThreshold = request.SuccessThreshold.Value;
        if (request.Description is not null) config.Description = request.Description;
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

    /// <summary>
    /// Resolves the check's declared <see cref="DimensionSpec"/> for a dimension name, so the alert rule
    /// copies its comparison kind and direction from the single source of truth (the check itself).
    /// Throws when the check doesn't declare that dimension — the same guard the old
    /// <c>AllowedAlertFors</c> gave, now driven by the check's own manifest.
    /// </summary>
    private DimensionSpec ResolveDimensionSpec(Check check, string dimension)
    {
        var checkImpl = checkRegistry.Find(check.Type.ToString())
            ?? throw new DomainValidationException($"No check implementation is registered for a {check.Type} check.");

        var spec = checkImpl.Manifest.Dimensions.FirstOrDefault(d => d.Name == dimension)
            ?? throw new DomainValidationException(
                $"\"{dimension}\" is not a valid alert dimension for a {check.Type} check.");

        return spec;
    }

    private async Task<Check> ResolveCheckAsync(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        return await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);
    }
}
