using System.Text.Json;
using Piro.Application.DTOs;
using Piro.Application.Extensions;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;
using Piro.Domain.Extensions;

namespace Piro.Application.Services;

/// <summary>
/// CRUD for notification subscriptions (RFC 0009 §4.4). Validates that chosen events belong to the
/// closed catalog and that the destination is complete and coherent with its <see cref="NotificationTargetKind"/>.
/// </summary>
public class NotificationSubscriptionAppService(
    INotificationSubscriptionRepository repo,
    IIntegrationRepository integrationRepo)
{
    /// <summary>The closed event catalog exposed to the subscription UI (RFC 0009 §4.2).</summary>
    public IReadOnlyList<NotificationEventCatalogDto> GetEventCatalog() =>
        Enum.GetValues<NotificationEventType>()
            .Select(e => new NotificationEventCatalogDto(e.WireName(), e.Description()))
            .ToList();

    public async Task<NotificationSubscriptionPageDto> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repo.GetPagedAsync(page, pageSize, ct);
        return new NotificationSubscriptionPageDto(
            items.Select(s => s.ToDto()),
            total,
            Math.Max(1, page),
            Math.Clamp(pageSize, 10, 200));
    }

    public async Task<NotificationSubscriptionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationSubscription), id.ToString());
        return sub.ToDto();
    }

    public async Task<NotificationSubscriptionDto> CreateAsync(UpsertNotificationSubscriptionRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(request, ct);

        var sub = new NotificationSubscription
        {
            Name = request.Name,
            EventsJson = JsonSerializer.Serialize(request.Events),
            MinSeverity = request.MinSeverity,
            TargetKind = request.TargetKind,
            UserId = request.UserId,
            IntegrationId = request.IntegrationId,
            Target = request.Target,
            Enabled = request.Enabled,
        };

        var created = await repo.CreateAsync(sub, ct);
        return created.ToDto();
    }

    public async Task<NotificationSubscriptionDto> UpdateAsync(Guid id, UpsertNotificationSubscriptionRequest request, CancellationToken ct = default)
    {
        _ = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationSubscription), id.ToString());
        await ValidateAsync(request, ct);

        var sub = new NotificationSubscription
        {
            Id = id,
            Name = request.Name,
            EventsJson = JsonSerializer.Serialize(request.Events),
            MinSeverity = request.MinSeverity,
            TargetKind = request.TargetKind,
            UserId = request.UserId,
            IntegrationId = request.IntegrationId,
            Target = request.Target,
            Enabled = request.Enabled,
        };

        var updated = await repo.UpdateAsync(sub, ct);
        return updated.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(NotificationSubscription), id.ToString());
        await repo.DeleteAsync(sub, ct);
    }

    private async Task ValidateAsync(UpsertNotificationSubscriptionRequest request, CancellationToken ct)
    {
        // Every event must be a known catalog wire name.
        foreach (var wireName in request.Events)
        {
            if (NotificationEventTypeExtensions.FromWireName(wireName) is null)
                throw new DomainValidationException($"Unknown notification event \"{wireName}\".");
        }

        // Destination must be complete and coherent with the declared kind.
        switch (request.TargetKind)
        {
            case NotificationTargetKind.Personal:
                if (request.UserId is null)
                    throw new DomainValidationException("A personal subscription requires a target user.");
                break;

            case NotificationTargetKind.Group:
            case NotificationTargetKind.Integration:
                if (request.IntegrationId is null)
                    throw new DomainValidationException($"A {request.TargetKind} subscription requires a target integration.");
                _ = await integrationRepo.GetByIdAsync(request.IntegrationId.Value, ct)
                    ?? throw new NotFoundException(nameof(Integration), request.IntegrationId.Value.ToString());
                break;

            default:
                throw new DomainValidationException($"Unsupported target kind {request.TargetKind}.");
        }
    }
}
