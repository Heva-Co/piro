using Piro.Application.DTOs;
using Piro.Contracts;
using Piro.Integrations.Abstractions;

namespace Piro.Application.Extensions;

/// <summary>
/// Projects an <see cref="IIntegration"/>'s manifest to the wire-level <see cref="IntegrationTypeMetaDto"/>,
/// reflecting its ConfigType via the shared <see cref="ConfigSchemaBuilder"/> (RFC 0016). The manifest
/// now comes from the integration's own class (via the registry), not from an enum attribute.
/// </summary>
public static class IntegrationManifestExtensions
{
    public static IntegrationTypeMetaDto ToMetaDto(this IIntegration integration)
    {
        var m = integration.Manifest;
        return new IntegrationTypeMetaDto(
            integration.IntegrationId,
            m.Label,
            m.Description,
            m.IconifyIcon,
            m.Category,
            m.ChannelOnly,
            m.Creatable,
            m.Direction,
            CapabilityNames(m.Capabilities),
            ConfigSchemaBuilder.For(m.ConfigType),
            m.WebhookPath
        );
    }

    /// <summary>Expands a capability flag set into its individual flag names, excluding None.</summary>
    private static IReadOnlyList<string> CapabilityNames(IntegrationCapability capabilities) =>
        Enum.GetValues<IntegrationCapability>()
            .Where(c => c != IntegrationCapability.None && capabilities.HasFlag(c))
            .Select(c => c.ToString())
            .ToArray();
}
