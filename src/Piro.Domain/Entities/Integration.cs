using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

public class Integration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IntegrationType Type { get; set; }
    public string? Description { get; set; }
    /// <summary>JSON blob with provider-specific credentials (e.g. Google service account JSON).</summary>
    public string ConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Check> Checks { get; set; } = [];
}
