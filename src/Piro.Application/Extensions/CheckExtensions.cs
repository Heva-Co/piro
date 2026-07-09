using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class CheckExtensions
{
    /// <summary>Maps a <see cref="Check"/> entity to its outbound DTO representation.</summary>
    public static CheckDto ToDto(this Check c) => new(
        c.Id, c.ServiceId, c.Slug, c.Name, c.Description,
        c.Type, c.Cron, c.TypeDataJson,
        c.CurrentStatus, c.IsActive, c.IsMultiRegion,
        c.FailureThreshold, c.RecoveryThreshold,
        c.HistoryDaysDesktop, c.HistoryDaysMobile,
        c.CreatedAt, c.UpdatedAt, c.IntegrationId,
        c.Criticality, c.AutomaticallyCreateIncident
    );
}
