using Piro.Application.Interfaces;

namespace Piro.Infrastructure;

/// <inheritdoc/>
internal class SiteUrlBuilder(ISiteConfigRepository siteConfigRepo) : ISiteUrlBuilder
{
    /// <inheritdoc/>
    public async Task<string?> GetUrlAsync(AdminArtifactType type, CancellationToken ct = default, params string[] identifiers)
    {
        var siteConfig = await siteConfigRepo.GetAsync(ct);
        var baseUrl = siteConfig.Url?.TrimEnd('/');
        if (baseUrl is null) return null;

        var path = type switch
        {
            AdminArtifactType.Service => $"/admin/services/{identifiers[0]}",
            AdminArtifactType.Check => $"/admin/services/{identifiers[0]}/checks/{identifiers[1]}",
            AdminArtifactType.Incident => $"/admin/incidents/{identifiers[0]}",
            AdminArtifactType.Alert => $"/admin/alerts/{identifiers[0]}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        return $"{baseUrl}{path}";
    }
}
