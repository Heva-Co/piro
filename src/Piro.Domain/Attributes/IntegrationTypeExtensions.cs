using System.Reflection;
using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

public static class IntegrationTypeExtensions
{
    public static bool IsChannelOnly(this IntegrationType type)
    {
        var attr = typeof(IntegrationType)
            .GetField(type.ToString())
            ?.GetCustomAttribute<IntegrationCategoryAttribute>();
        return attr?.ChannelOnly ?? false;
    }

    public static bool IsChannelOnly(this string typeName) =>
        Enum.TryParse(typeName, out IntegrationType parsed) && parsed.IsChannelOnly();
}
