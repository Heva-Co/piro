using System.Reflection;
using Piro.Domain.Attributes;
using Piro.Domain.Enums;

namespace Piro.Domain.Extensions;

public static class IntegrationTypeExtensions
{
    public static IntegrationCategory GetCategory(this IntegrationType type) =>
        typeof(IntegrationType)
            .GetField(type.ToString())
            ?.GetCustomAttribute<IntegrationCategoryAttribute>()
            ?.Category
        ?? IntegrationCategory.ThirdParty;
}
