namespace Piro.Application.Interfaces;

/// <summary>
/// Builds absolute admin-panel URLs for a given artifact, resolving the site's base URL
/// (<see cref="ISiteConfigRepository"/>) once in a single place — callers (notification dispatch,
/// escalation, etc.) shouldn't know the admin's route shape or need the base URL themselves.
/// </summary>
public interface ISiteUrlBuilder
{
    /// <summary>
    /// Returns the absolute admin URL for the given artifact, or null if no site URL is configured.
    /// <paramref name="identifiers"/> are the artifact's route segments in order, e.g.
    /// <c>GetUrlAsync(AdminArtifactType.Check, ct, serviceSlug, checkSlug)</c>.
    /// </summary>
    Task<string?> GetUrlAsync(AdminArtifactType type, CancellationToken ct = default, params string[] identifiers);
}

/// <summary>An admin-panel resource that can be linked to via <see cref="ISiteUrlBuilder"/>.</summary>
public enum AdminArtifactType
{
    /// <summary>Identifiers: serviceSlug.</summary>
    Service,

    /// <summary>Identifiers: serviceSlug, checkSlug.</summary>
    Check,

    /// <summary>Identifiers: incidentId.</summary>
    Incident,

    /// <summary>Identifiers: alertId.</summary>
    Alert,
}
