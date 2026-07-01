namespace Piro.Application.Models.TypeData;

public record GcpCloudRunJobCheckData
{
    public string ProjectId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    /// <summary>Mark DOWN if no completed execution exists within this many hours. Default 25 covers daily jobs with a 1-hour buffer.</summary>
    public int MaxAgeHours { get; init; } = 25;
}
