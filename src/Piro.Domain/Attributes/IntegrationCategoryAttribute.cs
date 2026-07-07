using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public sealed class IntegrationCategoryAttribute(IntegrationCategory category) : Attribute
{
    public IntegrationCategory Category { get; } = category;
}
