using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>
/// Identity claims extracted from an external identity provider (OIDC or SAML),
/// normalized to the minimal set Piro needs to provision a user.
/// </summary>
public record ExternalUserInfo(string Subject, string Email, string Name);

/// <summary>
/// Shared user provisioning for external SSO providers. Enforces the allowed-domain
/// policy, creates or links the local <c>AppUser</c>, and issues Piro's JWT pair.
/// Reused by both the OIDC and SAML2 sign-in flows so they follow an identical path.
/// </summary>
public interface ISsoUserProvisioner
{
    /// <summary>
    /// Validates <paramref name="info"/>'s email against <paramref name="allowedDomains"/>
    /// (comma-separated; null/empty = any), upserts the user under
    /// (<paramref name="providerId"/>, subject), assigns <paramref name="defaultRole"/> to
    /// brand-new users, and returns a signed-in response with Piro tokens.
    /// </summary>
    Task<SignInResponse> ProvisionAndSignInAsync(
        ExternalUserInfo info,
        string providerId,
        string defaultRole,
        string? allowedDomains,
        CancellationToken ct = default);
}
