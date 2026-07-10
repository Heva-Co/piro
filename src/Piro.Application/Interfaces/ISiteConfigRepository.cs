namespace Piro.Application.Interfaces;

public interface ISiteConfigRepository
{
    Task<SiteConfig> GetAsync(CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);

    /// <summary>Persists all of the given key/value pairs in a single transaction — either all apply or none do.</summary>
    Task SetManyAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default);
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
