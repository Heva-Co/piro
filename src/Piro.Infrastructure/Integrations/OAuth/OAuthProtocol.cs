namespace Piro.Infrastructure.Integrations.OAuth;

/// <summary>OAuth 2.0 protocol constant names, to avoid magic strings across the flow.</summary>
internal static class OAuthProtocol
{
    public const string ResponseType = "response_type";
    public const string ClientId = "client_id";
    public const string ClientSecret = "client_secret";
    public const string RedirectUri = "redirect_uri";
    public const string Scope = "scope";
    public const string State = "state";
    public const string Code = "code";
    public const string CodeChallenge = "code_challenge";
    public const string CodeChallengeMethod = "code_challenge_method";
    public const string CodeVerifier = "code_verifier";
    public const string GrantType = "grant_type";
    public const string RefreshToken = "refresh_token";

    public const string ResponseTypeCode = "code";
    public const string ChallengeMethodS256 = "S256";
    public const string GrantAuthorizationCode = "authorization_code";
    public const string GrantRefreshToken = "refresh_token";

    public const string AccessTokenField = "access_token";
    public const string RefreshTokenField = "refresh_token";
    public const string ExpiresInField = "expires_in";
    public const string ScopeField = "scope";
}
