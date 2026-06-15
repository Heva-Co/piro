using Microsoft.AspNetCore.Identity;

namespace Piro.Domain.Entities;

/// <summary>A named role that groups permissions together.</summary>
/// <remarks>
/// The "Owner" role is built-in and grants unrestricted access.
/// Use <c>UserManager.IsInRoleAsync(user, "Owner")</c> to check for ownership.
/// </remarks>
public class AppRole : IdentityRole<int>
{
    /// <summary>Built-in roles (Owner, Admin, Member, Viewer) cannot be deleted.</summary>
    public bool IsReadonly { get; set; }

    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
