using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class AppUserExtensions
{
    /// <summary>Maps an <see cref="AlertConfig"/> entity to its outbound DTO representation.</summary>
    public static UserProfileDto ToDto(this AppUser user, string[] roles, bool isOidc) => new(
       user.Id, user.Email!, user.Name, user.Color, user.TimeZone, roles, isOidc
    );
}
