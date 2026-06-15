using Microsoft.AspNetCore.Identity;

namespace Piro.Domain.Entities;

/// <summary>An authenticated user of the Piro admin panel.</summary>
/// <remarks>
/// Extends <see cref="IdentityUser{TKey}"/> so that password hashing, lockout,
/// and claims are handled by ASP.NET Core Identity.
/// Ownership is determined by membership in the built-in "Owner" role — there is no
/// separate <c>IsOwner</c> flag.
/// </remarks>
public class AppUser : IdentityUser<int>
{
    /// <summary>Display name shown in the UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Subject claim from the external identity provider (OIDC/SAML).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Identity provider name: "oidc" or "saml". Null for local accounts.</summary>
    public string? ExternalProvider { get; set; }

    /// <summary>Disabled users cannot sign in.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
