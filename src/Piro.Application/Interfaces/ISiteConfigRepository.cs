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
    IncidentCorrelationMode IncidentCorrelationMode = IncidentCorrelationMode.Merge,
    int MergeThreshold = 3,
    int MergeCorrelationWindowMinutes = 5
);

/// <summary>How Alerts are grouped into Incidents. Incidents never affect "all services" — they always
/// reflect exactly the set of services/checks/Alerts that are correlated.</summary>
public enum IncidentCorrelationMode
{
    /// <summary>Each service gets its own incident based on its own Alerts.</summary>
    PerService,

    /// <summary>Guarantees an incident always exists per-service, and merges recent per-service incidents
    /// into a single incident (reflecting exactly their combined services) once enough services are
    /// affected within the correlation window.</summary>
    Merge
}
