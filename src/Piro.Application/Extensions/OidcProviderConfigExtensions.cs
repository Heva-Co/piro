using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class OidcProviderConfigExtensions
{
    /// <summary>Maps an <see cref="OidcProviderConfig"/> entity to its outbound DTO representation. Never exposes the raw client secret — only whether one is set.</summary>
    public static OidcProviderConfigDto ToDto(this OidcProviderConfig p) =>
        new(p.Id, p.DisplayName, p.Authority, p.ClientId, !string.IsNullOrEmpty(p.ClientSecret), p.RedirectUri, p.Scopes, p.AllowedDomains, p.DefaultRole, p.IsEnabled);
}
