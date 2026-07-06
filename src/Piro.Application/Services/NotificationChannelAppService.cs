using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Exceptions;

namespace Piro.Application.Services;

/// <summary>Application service for notification channel CRUD.</summary>
public class NotificationChannelAppService(
    INotificationChannelRepository channelRepository,
    IAlertConfigRepository alertConfigRepository)
{
    public async Task<IEnumerable<NotificationChannelDto>> GetAllAsync(CancellationToken ct = default) =>
        (await channelRepository.GetAllAsync(ct)).Select(ToDto);

    public async Task<NotificationChannelDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var channel = await channelRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationChannel), id.ToString());
        return ToDto(channel);
    }

    public async Task<NotificationChannelDto> CreateAsync(CreateNotificationChannelRequest request, CancellationToken ct = default)
    {
        var channel = new NotificationChannel
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            MetaJson = request.MetaJson,
            IsGlobal = request.IsGlobal,
            IsLocked = request.IsLocked,
            IsInactive = request.IsInactive,
            IntegrationId = request.IntegrationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var created = await channelRepository.CreateAsync(channel, ct);

        if (created.IsGlobal)
            await PropagateToAllConfigsAsync(created, ct);

        return ToDto(created);
    }

    public async Task<NotificationChannelDto> UpdateAsync(int id, UpdateNotificationChannelRequest request, CancellationToken ct = default)
    {
        var channel = await channelRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationChannel), id.ToString());

        var wasGlobal = channel.IsGlobal;

        if (request.Name is not null) channel.Name = request.Name;
        if (request.Description is not null) channel.Description = request.Description;
        if (request.IsInactive is not null) channel.IsInactive = request.IsInactive.Value;
        if (request.MetaJson is not null) channel.MetaJson = request.MetaJson;
        if (request.IsGlobal is not null) channel.IsGlobal = request.IsGlobal.Value;
        if (request.IsLocked is not null) channel.IsLocked = request.IsLocked.Value;
        if (request.IntegrationId is not null) channel.IntegrationId = request.IntegrationId == 0 ? null : request.IntegrationId;
        channel.UpdatedAt = DateTime.UtcNow;

        var updated = await channelRepository.UpdateAsync(channel, ct);

        // Newly marked as global → propagate to all existing AlertConfigs
        if (!wasGlobal && updated.IsGlobal)
            await PropagateToAllConfigsAsync(updated, ct);

        return ToDto(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var channel = await channelRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationChannel), id.ToString());
        await channelRepository.DeleteAsync(channel, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task PropagateToAllConfigsAsync(NotificationChannel channel, CancellationToken ct)
    {
        var allConfigs = await alertConfigRepository.GetAllAsync(ct);
        foreach (var config in allConfigs)
        {
            if (config.AlertConfigNotificationChannels.Any(ac => ac.NotificationChannelId == channel.Id))
                continue;

            config.AlertConfigNotificationChannels.Add(new AlertConfigNotificationChannel
            {
                AlertConfigId = config.Id,
                NotificationChannelId = channel.Id,
            });
            await alertConfigRepository.UpdateAsync(config, ct);
        }
    }

    private static NotificationChannelDto ToDto(NotificationChannel c) => new(
        c.Id, c.Name, c.Type, c.Description, c.IsInactive, c.MetaJson,
        c.IsGlobal, c.IsLocked, c.CreatedAt, c.UpdatedAt,
        c.AlertConfigNotificationChannels.Count,
        c.IntegrationId,
        c.Integration?.Name
    );
}
