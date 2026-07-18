using System.ComponentModel.DataAnnotations;
using Piro.Domain.Attributes;

namespace Piro.Domain.Checks.Config;

public record GcpCloudRunJobCheckConfig
{
    [ConfigField("Project ID", Placeholder = "my-gcp-project", HelpText = "The GCP project the job runs in.")]
    [Required]
    public string ProjectId { get; init; } = string.Empty;

    [ConfigField("Region", Placeholder = "us-central1", HelpText = "The Cloud Run region.")]
    [Required]
    public string Region { get; init; } = string.Empty;

    [ConfigField("Job name", HelpText = "The Cloud Run Job to check.")]
    [Required]
    public string JobName { get; init; } = string.Empty;

    /// <summary>Mark DOWN if no completed execution exists within this many hours. Default 25 covers daily jobs with a 1-hour buffer.</summary>
    [ConfigField("Max age (hours)", HelpText = "Mark DOWN if no completed execution within this many hours. 25 covers daily jobs.")]
    public int MaxAgeHours { get; init; } = 25;
}
