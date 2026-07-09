using Piro.Domain.Enums;

namespace Piro.Domain.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public sealed class IntegrationCategoryAttribute(IntegrationCategory category) : Attribute
{
    public IntegrationCategory Category { get; } = category;

    /// <summary>
    /// When true, this integration type does not store global credentials.
    /// Configuration is provided per Notification Channel instead.
    /// </summary>
    public bool ChannelOnly { get; init; }
}
