using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class CheckDataPoinExtensions
{
    /// <summary>
    /// Maps a <see cref="CheckDataPoint"/> entity to its outbound DTO representation.
    /// </summary>
    public static CheckDataPointDto ToDto(this CheckDataPoint p) => new(
        p.Timestamp,
        p.Status.ToString(),
        p.LatencyMs,
        p.MetricValue,
        p.DataType?.ToString(),
        p.ErrorMessage,
        p.WorkerRegion
    );
}
