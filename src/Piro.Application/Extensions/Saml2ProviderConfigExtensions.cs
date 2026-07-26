using Piro.Application.DTOs;
using Piro.Domain.Entities;

namespace Piro.Application.Extensions;

public static class Saml2ProviderConfigExtensions
{
    /// <summary>Maps a <see cref="Saml2ProviderConfig"/> entity to its outbound DTO representation. The IdP signing certificate is public, but is still surfaced only as a boolean to keep the admin payload compact.</summary>
    public static Saml2ProviderConfigDto ToDto(this Saml2ProviderConfig p) =>
        new(p.Id, p.DisplayName, p.IdpEntityId, p.IdpSsoUrl, !string.IsNullOrEmpty(p.IdpSigningCertificate), p.SpEntityId, p.AllowedDomains, p.DefaultRole, p.IsEnabled);
}
