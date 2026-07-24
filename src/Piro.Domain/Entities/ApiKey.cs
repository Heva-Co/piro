using Piro.Domain.Enums;

namespace Piro.Domain.Entities;

/// <summary>A bearer token used for programmatic API access.</summary>
/// <remarks>Only the SHA-256 hash of the raw key is stored. The raw key is shown once on creation.</remarks>
public class ApiKey
{
    public int Id { get; set; }

    /// <summary>The user who created this key. Null for system-generated keys.</summary>
    public int? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the raw API key.</summary>
    public string HashedKey { get; set; } = string.Empty;

    /// <summary>Partially redacted key shown in the UI (e.g. "pk_****abc").</summary>
    public string MaskedKey { get; set; } = string.Empty;

    public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>When this key last successfully authenticated a request, if ever.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>What this key authorizes. Full = user credential; CheckInbound = one-check inbound token.</summary>
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.Full;

    /// <summary>The check a CheckInbound-scoped key is bound to. Null for Full keys.</summary>
    public int? CheckId { get; set; }

    public AppUser? User { get; set; }
}
