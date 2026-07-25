package co.heva.piro.shared.model

import kotlinx.serialization.Serializable

/**
 * Wire models mirroring the Piro API DTOs (src/Piro.Application/DTOs/AuthDto.cs and friends). Kept as a
 * hand-written mirror on the client side; field names match the API's camelCase JSON exactly.
 */

@Serializable
data class SignInRequest(val email: String, val password: String)

@Serializable
data class SignInResponse(
    val accessToken: String,
    val refreshToken: String,
    val expiresIn: Int,
    val user: UserDto,
)

@Serializable
data class UserDto(
    val id: Int,
    val email: String,
    val name: String,
    val roles: List<String> = emptyList(),
)

@Serializable
data class RefreshRequest(val refreshToken: String)

/** Richer profile returned by GET /api/v1/auth/me (UserProfileDto). */
@Serializable
data class UserProfile(
    val id: Int,
    val email: String,
    val name: String,
    val color: String = "",
    val timeZone: String = "UTC",
    val roles: List<String> = emptyList(),
    val isOidc: Boolean = false,
    val hasSeenShowcase: Boolean = false,
)

/** GET /api/v1/auth/oidc/sso-mode → { ssoOnly }. */
@Serializable
data class SsoMode(val ssoOnly: Boolean)

/**
 * One entry of GET /api/v1/auth/oidc/providers (OidcProviderInfo) — an enabled SSO button on the
 * sign-in screen. [id] is the provider key passed to /oidc/start?provider={id}.
 */
@Serializable
data class OidcProvider(
    val id: String,
    val displayName: String = "",
)

/** Body of POST /api/v1/auth/oidc/callback (OidcCallbackRequest). */
@Serializable
data class OidcCallbackRequest(val code: String, val state: String)
