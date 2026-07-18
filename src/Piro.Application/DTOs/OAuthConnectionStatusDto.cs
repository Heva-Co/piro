namespace Piro.Application.DTOs;

/// <summary>Whether an integration has a live OAuth connection, for the Connect/Disconnect UI.</summary>
public record OAuthConnectionStatusDto(
    bool Connected,
    DateTime? ExpiresAt,
    string? Scopes
);
