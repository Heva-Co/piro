using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class ServiceExtensions
{
    /// <summary>Maps a <see cref="Service"/> entity to its outbound DTO representation.</summary>
    public static ServiceDto ToDto(this Service s, int checkCount = 0) => new(
        s.Id, s.Slug, s.Name, s.Description, s.ImageUrl,
        s.CurrentStatus, s.DefaultStatus, s.IsHidden, s.DisplayOrder,
        s.HistoryDaysDesktop, s.HistoryDaysMobile,
        s.CreatedAt, s.UpdatedAt, checkCount
    );
}
