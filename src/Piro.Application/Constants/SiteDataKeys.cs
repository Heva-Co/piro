namespace Piro.Application.Constants;

/// <summary>Key constants for the <c>SiteData</c> key-value store.</summary>
public static class SiteDataKeys
{
    public const string SiteName = "site:name";
    public const string SiteUrl = "site:url";
    public const string SiteLogoUrl = "site:logo_url";
    public const string SiteFaviconUrl = "site:favicon_url";
    public const string SiteMetaTitle = "site:meta_title";
    public const string SiteMetaDescription = "site:meta_description";
    public const string SiteOgImageUrl = "site:og_image_url";

    public const string WorkerBuiltinDisabled = "worker:builtin_disabled";

    public const string IncidentCorrelationMode = "incidents:correlation_mode";
    public const string IncidentGlobalThreshold = "incidents:global_threshold";
    public const string IncidentGlobalCorrelationWindowMinutes = "incidents:global_correlation_window_minutes";

    public static readonly string[] All =
    [
        SiteName,
        SiteUrl,
        SiteLogoUrl,
        SiteFaviconUrl,
        SiteMetaTitle,
         SiteMetaDescription,
         SiteOgImageUrl,
        WorkerBuiltinDisabled,
        IncidentCorrelationMode,
        IncidentGlobalThreshold,
        IncidentGlobalCorrelationWindowMinutes,
    ];
}
