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
    string? OgImageUrl,
    bool BuiltinWorkerDisabled = false,
    int IncidentPublishDelayMinutes = 0,
    IncidentCorrelationMode IncidentCorrelationMode = IncidentCorrelationMode.Hybrid,
    int GlobalIncidentThreshold = 3,
    int GlobalIncidentCorrelationWindowMinutes = 5
);

public enum IncidentCorrelationMode
{
    PerService,
    Global,
    Hybrid
}
