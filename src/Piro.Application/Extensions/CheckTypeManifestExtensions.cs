using Piro.Application.DTOs;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;
using Piro.Contracts;

namespace Piro.Application.Extensions;

/// <summary>
/// Projects a <see cref="CheckType"/>'s manifest (RFC 0011) into its wire-level
/// <see cref="CheckTypeMetaDto"/> — display metadata plus the reflected config schema (via the
/// shared <see cref="ConfigSchemaBuilder"/>). Mirrors <see cref="IntegrationManifestExtensions"/>.
/// </summary>
public static class CheckTypeManifestExtensions
{
    /// <summary>
    /// Returns the wire-level manifest for this type, or null for a type with none (the
    /// not-yet-implemented Heartbeat / GRPC). <paramref name="hasExecutor"/> says whether a runnable
    /// executor is registered for it.
    /// </summary>
    public static CheckTypeMetaDto? ToMetaDto(this CheckType type, bool hasExecutor)
    {
        var manifest = type.GetManifest();
        if (manifest is null)
            return null;

        return new CheckTypeMetaDto(
            type.ToString(),
            manifest.DisplayName,
            manifest.Description,
            manifest.MinIntervalSeconds,
            manifest.AllowedAlertFors.Select(a => a.ToString()).ToArray(),
            ConfigSchemaBuilder.For(manifest.ConfigType),
            manifest.RequiredIntegration?.ToString(),
            hasExecutor
        );
    }
}
