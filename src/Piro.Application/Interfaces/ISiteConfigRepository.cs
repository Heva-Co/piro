namespace Piro.Application.Interfaces;

public interface ISiteConfigRepository
{
    Task<SiteConfig> GetAsync(CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
}

public record SiteConfig(
    string? Name,
    string? Url,
    string? LogoUrl,
    string? FaviconUrl,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImageUrl
);
