using System.Reflection;
using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

public static class IntegrationTypeExtensions
{
    /// <summary>
    /// Whether this type's manifest marks it as not storing global credentials — configuration is
    /// provided per Notification Channel instead.
    /// </summary>
    public static bool IsChannelOnly(this IntegrationType type) =>
        type.GetManifest()?.ChannelOnly ?? false;

    /// <summary>
    /// String-keyed overload of <see cref="IsChannelOnly(IntegrationType)"/> for callers that only
    /// have the type name (e.g. from an untyped request payload). Returns false if the name doesn't
    /// parse to a known <see cref="IntegrationType"/>.
    /// </summary>
    public static bool IsChannelOnly(this string typeName) =>
        Enum.TryParse(typeName, out IntegrationType parsed) && parsed.IsChannelOnly();

    /// <summary>
    /// Returns this type's declared manifest, or null for a type with none (e.g. the obsolete ones).
    /// </summary>
    public static IntegrationManifestAttribute? GetManifest(this IntegrationType type) =>
        typeof(IntegrationType)
            .GetField(type.ToString())
            ?.GetCustomAttribute<IntegrationManifestAttribute>();

    /// <summary>
    /// Returns the actions declared on this type via <see cref="IntegrationActionAttribute"/> (RFC 0012),
    /// in declaration order. Empty for a type that declares none. These are metadata only — the behavior
    /// lives in a matching IIntegrationAction handler, joined by (type, ActionId).
    /// </summary>
    public static IReadOnlyList<IntegrationActionAttribute> GetActions(this IntegrationType type) =>
        typeof(IntegrationType)
            .GetField(type.ToString())
            ?.GetCustomAttributes<IntegrationActionAttribute>().ToList()
            ?? [];
}
