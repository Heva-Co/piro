namespace Piro.Application.DTOs;

/// <summary>Snapshot of a scheduled Quartz job/trigger.</summary>
public record JobStatusDto(
    string JobGroup,
    string JobName,
    string TriggerGroup,
    string TriggerName,
    string State,
    DateTimeOffset? NextFireTimeUtc,
    DateTimeOffset? PreviousFireTimeUtc,
    CheckRefDto? Check
);

/// <summary>Minimal check reference attached to a job, so the UI can link to it.</summary>
public record CheckRefDto(
    int Id,
    string Name,
    string Slug,
    string ServiceSlug
);
