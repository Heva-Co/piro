namespace Piro.Domain.Entities;

/// <summary>Generic key-value store for instance-level configuration (name, logo, etc.).</summary>
public class SiteData
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>Hint for deserialization: "string", "json", "boolean", "number".</summary>
    public string DataType { get; set; } = "string";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
