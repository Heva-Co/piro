using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class DependencyExtensions
{
    /// <summary>Maps a <see cref="ServiceDependency"/> edge to its outbound DTO representation.</summary>
    public static DependencyDto ToDto(this ServiceDependency d, string serviceSlug) => new(
        serviceSlug,
        d.DependsOnService?.Slug ?? d.DependsOnServiceId.ToString(),
        d.PropagationMode,
        d.CreatedAt
    );
}
