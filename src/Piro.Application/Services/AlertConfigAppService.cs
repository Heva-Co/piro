using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

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
        EnsureAlertForAllowed(check, request.AlertFor);

        var config = new AlertConfig
        {
            CheckId = check.Id,
            AlertFor = request.AlertFor,
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

        if (request.AlertFor is not null) EnsureAlertForAllowed(check, request.AlertFor.Value);
        if (request.AlertFor is not null) config.AlertFor = request.AlertFor.Value;
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

    private static void EnsureAlertForAllowed(Check check, Domain.Enums.AlertFor alertFor)
    {
        if (!check.Type.AllowedAlertFors().Contains(alertFor))
            throw new DomainValidationException(
                $"{alertFor} is not a valid alert metric for a {check.Type} check.");
    }

    private async Task<Check> ResolveCheckAsync(string serviceSlug, string checkSlug, CancellationToken ct)
    {
        var service = await serviceRepository.GetBySlugAsync(serviceSlug, ct)
            ?? throw new NotFoundException(nameof(Service), serviceSlug);
        return await checkRepository.GetBySlugAsync(service.Id, checkSlug, ct)
            ?? throw new NotFoundException(nameof(Check), checkSlug);
    }
}
