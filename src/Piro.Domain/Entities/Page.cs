namespace Piro.Domain.Entities;

/// <summary>A public-facing status page that displays a curated set of services.</summary>
public class Page
{
    public int Id { get; set; }

    /// <summary>URL path for this page, e.g. "/" or "/api".</summary>
    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Header { get; set; }
    public string? Subheader { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>JSON blob for theme, social links, and other display settings.</summary>
    public string? SettingsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PageService> PageServices { get; set; } = [];
}
