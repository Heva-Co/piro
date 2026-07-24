using Piro.Application.DTOs;
using Piro.Checks.Abstractions;
using Piro.Contracts;

namespace Piro.Application.Extensions;

/// <summary>
/// Projects a registered <see cref="ICheck"/>'s <see cref="CheckManifest"/> into its wire-level
/// <see cref="CheckTypeMetaDto"/> — display metadata, its dimensions, and the reflected config schema
/// (via the shared <see cref="ConfigSchemaBuilder"/>). Mirrors <see cref="IntegrationManifestExtensions"/>.
/// </summary>
public static class CheckTypeManifestExtensions
{
    /// <summary>Returns the wire-level manifest for a registered check.</summary>
    public static CheckTypeMetaDto ToMetaDto(this ICheck check)
    {
        var manifest = check.Manifest;
        return new CheckTypeMetaDto(
            check.CheckId,
            manifest.Label,
            manifest.Description,
            manifest.DefaultIntervalSeconds,
            [.. manifest.Dimensions.Select(d => new CheckDimensionDto(d.Name, d.Comparison, d.Direction, d.Unit))],
            ConfigSchemaBuilder.For(manifest.ConfigType),
            manifest.RequiredIntegration,
            HasExecutor: true,
            SingleRegionOnly: manifest.SingleRegionOnly
        );
    }
}
