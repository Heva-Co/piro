namespace Piro.Domain.Entities;

/// <summary>Junction granting a <see cref="Permission"/> to an <see cref="AppRole"/>.</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public string PermissionId { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }

    public AppRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
