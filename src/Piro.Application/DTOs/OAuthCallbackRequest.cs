namespace Piro.Application.DTOs;

/// <summary>Body of the integration OAuth callback — the code+state the SPA callback page relays back.</summary>
public record OAuthCallbackRequest(string Code, string State);
