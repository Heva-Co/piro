using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class AlertConfigExtensions
{
    /// <summary>Maps an <see cref="AlertConfig"/> entity to its outbound DTO representation.</summary>
    public static AlertConfigDto ToDto(this AlertConfig a) => new(
        a.Id, a.CheckId, a.Dimension, a.Comparison, a.Direction, a.AlertValue,
        a.FailureThreshold, a.SuccessThreshold,
        a.Description, a.IsActive, a.IsAlerting,
        a.Severity,
        a.CreatedAt, a.UpdatedAt
    );
}
