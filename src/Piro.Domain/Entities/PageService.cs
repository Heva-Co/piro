namespace Piro.Domain.Entities;

/// <summary>Junction between a <see cref="Page"/> and a <see cref="Service"/>, with display settings.</summary>
public class PageService
{
    public int PageId { get; set; }
    public int ServiceId { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>When true, the individual checks are shown expanded under the service on the page.</summary>
    public bool ShowChecks { get; set; }

    public string? SettingsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Page Page { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
