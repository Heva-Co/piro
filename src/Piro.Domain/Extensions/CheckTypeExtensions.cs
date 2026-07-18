using System.Reflection;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.Domain.Extensions;

public static class CheckTypeExtensions
{
    /// <summary>
    /// Returns this type's declared <see cref="CheckTypeManifestAttribute"/>, or null for a type
    /// with none (the not-yet-implemented <see cref="CheckType.Heartbeat"/> / <see cref="CheckType.GRPC"/>).
    /// Mirrors <c>IntegrationTypeExtensions.GetManifest</c>.
    /// </summary>
    public static CheckTypeManifestAttribute? GetManifest(this CheckType type) =>
        typeof(CheckType)
            .GetField(type.ToString())
            ?.GetCustomAttribute<CheckTypeManifestAttribute>();

    /// <summary>
    /// The <see cref="AlertFor"/> values that make sense for a given <see cref="CheckType"/> —
    /// e.g. a GCP Cloud Run Job check has no latency signal, and only SSL checks report
    /// <see cref="AlertFor.CertExpiry"/>. See RFC 0002 §4.4. Now sourced from the manifest (RFC 0011);
    /// the not-yet-implemented Heartbeat/GRPC types have no manifest and fall back to Status only.
    /// </summary>
    public static AlertFor[] AllowedAlertFors(this CheckType type) =>
        type.GetManifest()?.AllowedAlertFors ?? [AlertFor.Status];

    /// <summary>
    /// The minimum allowed interval between runs for this type, from its manifest (RFC 0011).
    /// Types without a manifest (Heartbeat/GRPC) have no declared floor.
    /// </summary>
    public static TimeSpan? MinInterval(this CheckType type)
    {
        var manifest = type.GetManifest();
        return manifest is null ? null : TimeSpan.FromSeconds(manifest.MinIntervalSeconds);
    }

    /// <summary>
    /// Throws <see cref="DomainValidationException"/> if <paramref name="interval"/> is tighter than
    /// this type's declared minimum (RFC 0011). No-op for a type with no manifest.
    /// </summary>
    public static void EnsureIntervalAllowed(this CheckType type, TimeSpan interval)
    {
        var manifest = type.GetManifest();
        if (manifest is null) return;

        var min = TimeSpan.FromSeconds(manifest.MinIntervalSeconds);
        if (interval < min)
            throw new DomainValidationException(
                $"{manifest.DisplayName} checks must run no more often than every {min.TotalMinutes:0} minute(s).");
    }
}
