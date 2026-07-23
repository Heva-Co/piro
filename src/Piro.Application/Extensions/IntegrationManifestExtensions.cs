using Piro.Application.DTOs;
using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Application.Extensions;

/// <summary>
/// Builds the wire-level <see cref="IntegrationTypeMetaDto"/> for an IntegrationType by reflecting
/// over its manifest ConfigType (see IntegrationManifestAttribute) via the shared
/// <see cref="ConfigSchemaBuilder"/> — the schema is derived, never hand-authored, so it can't
/// drift from what the code actually deserializes.
/// </summary>
public static class IntegrationManifestExtensions
{
    /// <summary>
    /// Returns the wire-level manifest for this type, or null for a type with none (e.g. the
    /// obsolete ones).
    /// </summary>
    public static IntegrationTypeMetaDto? ToMetaDto(this IntegrationType type)
    {
        var manifest = type.GetManifest();
        if (manifest is null)
            return null;

        return new IntegrationTypeMetaDto(
            type.ToString(),
            manifest.Label,
            manifest.Description,
            manifest.IconifyIcon,
            manifest.Category,
            manifest.ChannelOnly,
            manifest.Creatable,
            manifest.Direction,
            CapabilityNames(manifest.Capabilities),
            ConfigSchemaBuilder.For(manifest.ConfigType),
            manifest.WebhookPath
        );
    }

    /// <summary>
    /// Expands a <see cref="IntegrationCapability"/> flag set into its individual flag names,
    /// excluding <see cref="IntegrationCapability.None"/>.
    /// </summary>
    private static IReadOnlyList<string> CapabilityNames(IntegrationCapability capabilities) =>
        Enum.GetValues<IntegrationCapability>()
            .Where(c => c != IntegrationCapability.None && capabilities.HasFlag(c))
            .Select(c => c.ToString())
            .ToArray();
}
