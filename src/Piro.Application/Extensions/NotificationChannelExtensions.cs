using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class NotificationChannelExtensions
{
    /// <summary>Maps a <see cref="NotificationChannel"/> entity to its outbound DTO representation.</summary>
    public static NotificationChannelDto ToDto(this NotificationChannel c) => new(
        c.Id, c.Name, c.Type, c.Description, c.IsInactive, c.MetaJson,
        c.IsGlobal, c.IsLocked, c.CreatedAt, c.UpdatedAt,
        c.IntegrationId,
        c.Integration?.Name
    );
}
