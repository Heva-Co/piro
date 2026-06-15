namespace Piro.Domain.Entities;

/// <summary>A bearer token used for programmatic API access.</summary>
/// <remarks>Only the HMAC-SHA256 hash of the raw key is stored. The raw key is shown once on creation.</remarks>
public class ApiKey
{
    public int Id { get; set; }

    /// <summary>The user who created this key. Null for system-generated keys.</summary>
    public int? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 hash of the raw API key.</summary>
    public string HashedKey { get; set; } = string.Empty;

    /// <summary>Partially redacted key shown in the UI (e.g. "pk_****abc").</summary>
    public string MaskedKey { get; set; } = string.Empty;

    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AppUser? User { get; set; }
}
