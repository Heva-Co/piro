using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for <see cref="AlertConfig"/> CRUD within a check.</summary>
public class AlertConfigAppService(
    IAlertConfigRepository alertConfigRepository,
    ICheckRepository checkRepository,
    IServiceRepository serviceRepository,
    INotificationChannelRepository channelRepository)
{
    public async Task<IEnumerable<AlertConfigDto>> GetByCheckAsync(
        string serviceSlug, string checkSlug, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var configs = await alertConfigRepository.GetByCheckIdAsync(check.Id, ct);
        return configs.Select(ToDto);
    }

    public async Task<AlertConfigDto> GetByIdAsync(
        string serviceSlug, string checkSlug, int id, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);
        var config = await alertConfigRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(AlertConfig), id.ToString());
        if (config.CheckId != check.Id) throw new NotFoundException(nameof(AlertConfig), id.ToString());
        return ToDto(config);
    }

    public async Task<AlertConfigDto> CreateAsync(
        string serviceSlug, string checkSlug, CreateAlertConfigRequest request, CancellationToken ct = default)
    {
        var check = await ResolveCheckAsync(serviceSlug, checkSlug, ct);

        var config = new AlertConfig
        {
            CheckId = check.Id,
            AlertFor = request.AlertFor,
            AlertValue = request.AlertValue,
            FailureThreshold = request.FailureThreshold,
            SuccessThreshold = request.SuccessThreshold,
            Description = request.Description,
            CreateIncident = request.CreateIncident,
            IsActive = request.IsActive,
            Severity = request.Severity
        };

        var created = await alertConfigRepository.CreateAsync(config, ct);

        // Merge requested channels with all global channels
        var globalChannels = await channelRepository.GetGlobalAsync(ct);
        var mergedIds = (request.NotificationChannelIds ?? [])
            .Union(globalChannels.Select(c => c.Id))
            .ToList();
        await SyncChannelsAsync(created, mergedIds, ct);

        var dto = ToDto(await alertConfigRepository.GetByIdAsync(created.Id, ct) ?? created);
        return dto;
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
        if (request.IsActive is not null) config.IsActive = request.IsActive.Value;
        if (request.Severity is not null) config.Severity = request.Severity.Value;

        await alertConfigRepository.UpdateAsync(config, ct);

        if (request.NotificationChannelIds is not null)
            await SyncChannelsAsync(config, request.NotificationChannelIds, ct);

        return ToDto(await alertConfigRepository.GetByIdAsync(id, ct) ?? config);
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

    /// <summary>Replaces the notification channel associations on an alert config.</summary>
    private async Task SyncChannelsAsync(AlertConfig config, List<int> channelIds, CancellationToken ct)
    {
        var existing = config.AlertConfigNotificationChannels.ToList();
        var existingIds = existing.Select(ac => ac.NotificationChannelId).ToHashSet();
        var requestedIds = channelIds.ToHashSet();

        // Remove channels no longer in the list — skip locked ones
        foreach (var ac in existing.Where(ac => !requestedIds.Contains(ac.NotificationChannelId)))
        {
            if (ac.NotificationChannel?.IsLocked == true) continue;
            config.AlertConfigNotificationChannels.Remove(ac);
        }

        // Add new channels
        foreach (var channelId in requestedIds.Where(id => !existingIds.Contains(id)))
        {
            var channel = await channelRepository.GetByIdAsync(channelId, ct)
                ?? throw new NotFoundException(nameof(NotificationChannel), channelId.ToString());
            config.AlertConfigNotificationChannels.Add(new AlertConfigNotificationChannel
            {
                AlertConfigId = config.Id,
                NotificationChannelId = channel.Id
            });
        }

        await alertConfigRepository.UpdateAsync(config, ct);
    }

    private static AlertConfigDto ToDto(AlertConfig a) => new(
        a.Id, a.CheckId, a.AlertFor, a.AlertValue,
        a.FailureThreshold, a.SuccessThreshold,
        a.Description, a.CreateIncident, a.IsActive, a.IsAlerting,
        a.Severity,
        a.AlertConfigNotificationChannels.Select(ac => ac.NotificationChannelId).ToList(),
        a.CreatedAt, a.UpdatedAt
    );
}
