namespace Piro.Domain.Entities;

/// <summary>A user in an <see cref="OnCallLayer"/> rotation, ordered by <see cref="Position"/>.</summary>
public class OnCallLayerUser
{
    public int Id { get; set; }
    public int LayerId { get; set; }
    public OnCallLayer Layer { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>0-based position in the rotation sequence.</summary>
    public int Position { get; set; }
}
