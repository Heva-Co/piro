using Piro.Domain.Attributes;
using Piro.Contracts;
using Piro.Domain.Enums;

namespace Piro.Domain.Extensions;

public static class IntegrationTypeExtensions
{
    /// <summary>
    /// Whether this type is a service/action integration (ThirdParty) or a notification channel
    /// (Notification), per its manifest. Falls back to <see cref="IntegrationCategory.ThirdParty"/>
    /// for a type with no manifest.
    /// </summary>
    public static IntegrationCategory GetCategory(this IntegrationType type) =>
        type.GetManifest()?.Category ?? IntegrationCategory.ThirdParty;
}
