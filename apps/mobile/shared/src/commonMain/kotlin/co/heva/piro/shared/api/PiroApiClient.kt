package co.heva.piro.shared.api

import co.heva.piro.shared.auth.TokenStorage
import co.heva.piro.shared.model.AlertDetail
import co.heva.piro.shared.model.AlertListEnvelope
import co.heva.piro.shared.model.DeviceDto
import co.heva.piro.shared.model.OidcCallbackRequest
import co.heva.piro.shared.model.OidcProvider
import co.heva.piro.shared.model.RefreshRequest
import co.heva.piro.shared.model.RegisterDeviceRequest
import co.heva.piro.shared.model.SignInRequest
import co.heva.piro.shared.model.SignInResponse
import co.heva.piro.shared.model.SsoMode
import co.heva.piro.shared.model.UserProfile
import co.heva.piro.shared.model.generated.UpdateProfileRequest
import co.heva.piro.shared.model.generated.UserNotificationPreferenceDto
import io.ktor.client.HttpClient
import io.ktor.client.call.body
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.delete
import io.ktor.client.request.get
import io.ktor.client.request.header
import io.ktor.client.request.parameter
import io.ktor.client.request.post
import io.ktor.client.request.put
import io.ktor.client.request.setBody
import io.ktor.client.statement.HttpResponse
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.contentType
import io.ktor.http.isSuccess
import io.ktor.serialization.kotlinx.json.json
import kotlinx.serialization.json.Json

/**
 * The single client the app uses to talk to the Piro API. It attaches the bearer access token, and on
 * a 401 transparently refreshes with the stored refresh token (rotating both) and retries once. All
 * calls return typed models; a failed call throws [PiroApiException].
 *
 * [baseUrl] is the API root without a trailing slash, e.g. "http://10.0.2.2:5117" (the Android
 * emulator's alias for the host machine).
 */
class PiroApiClient(
    private val baseUrl: String,
    private val tokens: TokenStorage,
) {
    // Built internally from the platform engine so callers never touch a Ktor type — the app module
    // depends only on this client, not on Ktor.
    private val http: HttpClient = platformHttpClient().config {
        install(ContentNegotiation) {
            json(Json { ignoreUnknownKeys = true; isLenient = true })
        }
        expectSuccess = false
    }

    // --- Auth ---
    //
    // Every public suspend call is annotated `@Throws(Throwable::class)`: without it, a Kotlin exception
    // crossing into Swift terminates the process instead of surfacing as a Swift `throws`. The annotation
    // is metadata-only on Android (no behavior change) and lets the iOS app `try await` these calls.

    @Throws(Throwable::class)
    suspend fun signIn(email: String, password: String): SignInResponse {
        val response = http.post("$baseUrl/api/v1/auth/sign-in") {
            contentType(ContentType.Application.Json)
            setBody(SignInRequest(email, password))
        }
        if (!response.status.isSuccess()) throw response.toException("Sign-in failed")
        val result: SignInResponse = response.body()
        tokens.accessToken = result.accessToken
        tokens.refreshToken = result.refreshToken
        return result
    }

    @Throws(Throwable::class)
    suspend fun me(): UserProfile = authorizedGet("$baseUrl/api/v1/auth/me")

    /** Updates the signed-in user's display name, avatar color and/or time zone (PUT /api/v1/auth/me). */
    @Throws(Throwable::class)
    suspend fun updateProfile(name: String?, color: String?, timeZone: String?): UserProfile {
        val response = authorized {
            http.put("$baseUrl/api/v1/auth/me") {
                bearer(it)
                contentType(ContentType.Application.Json)
                setBody(UpdateProfileRequest(name = name, color = color, timeZone = timeZone))
            }
        }
        if (!response.status.isSuccess()) throw response.toException("Could not update profile")
        return response.body()
    }

    /** The user's notification-delivery preferences (GET /api/v1/users/{userId}/notification-preferences). */
    @Throws(Throwable::class)
    suspend fun getNotificationPreferences(userId: Int): List<UserNotificationPreferenceDto> =
        authorizedGet("$baseUrl/api/v1/users/$userId/notification-preferences")

    @Throws(Throwable::class)
    suspend fun signOut() {
        runCatching { authorized { http.post("$baseUrl/api/v1/auth/sign-out") { bearer(it) } } }
        tokens.clear()
    }

    // --- SSO / OIDC ---

    @Throws(Throwable::class)
    suspend fun getSsoMode(): SsoMode {
        val response = http.get("$baseUrl/api/v1/auth/oidc/sso-mode")
        if (!response.status.isSuccess()) throw response.toException("Could not load SSO mode")
        return response.body()
    }

    @Throws(Throwable::class)
    suspend fun getOidcProviders(): List<OidcProvider> {
        val response = http.get("$baseUrl/api/v1/auth/oidc/providers")
        if (!response.status.isSuccess()) throw response.toException("Could not load SSO providers")
        return response.body()
    }

    /** Builds the URL the system browser opens to start an SSO login for [providerId]. */
    fun oidcStartUrl(providerId: String): String = "$baseUrl/api/v1/auth/oidc/start?provider=$providerId"

    /** Completes SSO after the browser redirect: exchanges the code+state for the token pair. */
    @Throws(Throwable::class)
    suspend fun completeOidcCallback(code: String, state: String): SignInResponse {
        val response = http.post("$baseUrl/api/v1/auth/oidc/callback") {
            contentType(ContentType.Application.Json)
            setBody(OidcCallbackRequest(code, state))
        }
        if (!response.status.isSuccess()) throw response.toException("SSO sign-in failed")
        val result: SignInResponse = response.body()
        tokens.accessToken = result.accessToken
        tokens.refreshToken = result.refreshToken
        return result
    }

    // --- Devices ---

    @Throws(Throwable::class)
    suspend fun registerDevice(platform: String, token: String, deviceName: String?): DeviceDto =
        authorized {
            http.post("$baseUrl/api/v1/devices") {
                bearer(it)
                contentType(ContentType.Application.Json)
                setBody(RegisterDeviceRequest(platform, token, deviceName))
            }
        }.let { response ->
            if (!response.status.isSuccess()) throw response.toException("Device registration failed")
            response.body()
        }

    @Throws(Throwable::class)
    suspend fun getDevices(): List<DeviceDto> = authorizedGet("$baseUrl/api/v1/devices")

    // --- Alerts ---

    /** Active + recent alerts (GET /api/v1/alerts). The API returns a paged envelope; we read its items. */
    @Throws(Throwable::class)
    suspend fun getAlerts(): List<AlertDetail> {
        val response = authorized { http.get("$baseUrl/api/v1/alerts") { bearer(it) } }
        if (!response.status.isSuccess()) throw response.toException("Could not load alerts")
        val envelope: AlertListEnvelope = response.body()
        return envelope.items
    }

    @Throws(Throwable::class)
    suspend fun getAlert(id: Int): AlertDetail = authorizedGet("$baseUrl/api/v1/alerts/$id")

    /** Acknowledges an alert, pausing its escalation. Returns the updated alert. */
    @Throws(Throwable::class)
    suspend fun acknowledgeAlert(id: Int): AlertDetail {
        val response = authorized { http.post("$baseUrl/api/v1/alerts/$id/acknowledge") { bearer(it) } }
        if (!response.status.isSuccess()) throw response.toException("Acknowledge failed")
        return response.body()
    }

    @Throws(Throwable::class)
    suspend fun deleteDevice(token: String) {
        val response = authorized {
            http.delete("$baseUrl/api/v1/devices") {
                bearer(it)
                parameter("token", token)
            }
        }
        if (!response.status.isSuccess()) throw response.toException("Device removal failed")
    }

    // --- Auth plumbing: attach bearer, refresh-on-401, retry once ---

    private suspend inline fun <reified T> authorizedGet(url: String): T {
        val response = authorized { http.get(url) { bearer(it) } }
        if (!response.status.isSuccess()) throw response.toException("Request failed")
        return response.body()
    }

    /**
     * Runs [block] with the current access token. If it 401s, refreshes once and retries. A refresh
     * failure clears the tokens and surfaces as an auth error the UI treats as "log in again".
     */
    private suspend fun authorized(block: suspend (accessToken: String?) -> HttpResponse): HttpResponse {
        val first = block(tokens.accessToken)
        if (first.status != HttpStatusCode.Unauthorized) return first

        if (!refresh()) throw PiroApiException(HttpStatusCode.Unauthorized.value, "Session expired")
        return block(tokens.accessToken)
    }

    private suspend fun refresh(): Boolean {
        val refreshToken = tokens.refreshToken ?: return false
        val response = http.post("$baseUrl/api/v1/auth/refresh") {
            contentType(ContentType.Application.Json)
            setBody(RefreshRequest(refreshToken))
        }
        if (!response.status.isSuccess()) {
            tokens.clear()
            return false
        }
        val result: SignInResponse = response.body()
        tokens.accessToken = result.accessToken
        tokens.refreshToken = result.refreshToken
        return true
    }

    private fun io.ktor.client.request.HttpRequestBuilder.bearer(accessToken: String?) {
        if (accessToken != null) header(HttpHeaders.Authorization, "Bearer $accessToken")
    }

    private suspend fun HttpResponse.toException(fallback: String): PiroApiException {
        val body = runCatching { bodyAsErrorText() }.getOrNull()
        return PiroApiException(status.value, body ?: fallback)
    }

    private suspend fun HttpResponse.bodyAsErrorText(): String? {
        val text = body<Map<String, kotlinx.serialization.json.JsonElement>>()
        return (text["title"] ?: text["message"])?.toString()?.trim('"')
    }
}

/** A non-success API response. [status] is the HTTP status; [message] is the API's error title, if any. */
class PiroApiException(val status: Int, message: String) : Exception(message)
