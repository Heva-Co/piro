namespace Piro.Domain.Entities;

/// <summary>A discrete action that can be granted to a <see cref="Role"/>.</summary>
public class Permission
{
    public string Id { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
