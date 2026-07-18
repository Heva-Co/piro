namespace Piro.Application.DTOs;

/// <summary>The backend-resolved OAuth redirect URL to register in the provider's app.</summary>
public record OAuthRedirectUriDto(string RedirectUri);
