namespace Piro.Application.DTOs;

/// <summary>Credentials for local sign-in.</summary>
public record SignInRequest(string Email, string Password);

/// <summary>Tokens returned after successful authentication.</summary>
public record SignInResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserDto User
);

/// <summary>Request to exchange a refresh token for a new access token.</summary>
public record RefreshRequest(string RefreshToken);

/// <summary>Outbound representation of the authenticated user.</summary>
public record UserDto(int Id, string Email, string Name, IEnumerable<string> Roles);

/// <summary>Request to create an API key.</summary>
public record CreateApiKeyRequest(string Name);

/// <summary>Returned once when an API key is created. The raw key is never stored and cannot be retrieved again.</summary>
public record ApiKeyCreatedResponse(int Id, string Name, string RawKey, string MaskedKey, DateTime CreatedAt);

/// <summary>Safe representation of an API key (no raw key).</summary>
public record ApiKeyDto(int Id, string Name, string MaskedKey, string Status, DateTime CreatedAt);
