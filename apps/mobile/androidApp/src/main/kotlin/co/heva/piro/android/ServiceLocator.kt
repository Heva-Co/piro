package co.heva.piro.android

import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.auth.EncryptedTokenStorage
import co.heva.piro.shared.auth.TokenStorage
import android.content.Context

/**
 * Minimal hand-rolled dependency container — the app is small enough that a DI framework would be
 * overkill. Holds the single [TokenStorage] and the current [PiroApiClient].
 *
 * The API client is rebuilt rather than mutated when the user points the app at a different server:
 * [PiroApiClient] takes its base URL at construction, and a client whose host could change mid-flight
 * would let an in-flight request land on the wrong server.
 */
class ServiceLocator(context: Context) {

    private val appContext = context.applicationContext

    val tokenStorage: TokenStorage = EncryptedTokenStorage(appContext)

    val serverStore = ServerStore(appContext)

    /**
     * The server the app currently talks to. Falls back to the build-time default, which is what keeps a
     * dev build working against localhost with nothing typed.
     */
    var baseUrl: String = serverStore.baseUrl ?: BuildConfig.PIRO_API_BASE_URL
        private set

    var api: PiroApiClient = PiroApiClient(baseUrl = baseUrl, tokens = tokenStorage)
        private set

    /**
     * Points the app at [rawUrl], persisting it and rebuilding the API client. Returns the normalized
     * URL, or null when the input is not a usable http(s) address.
     *
     * A change of host clears any stored session: tokens are issued by one server and are meaningless to
     * another, so carrying them over would send a stale credential to a new host.
     */
    fun useServer(rawUrl: String): String? {
        val normalized = ServerStore.normalize(rawUrl) ?: return null

        // Nothing to do when it resolves to the same host we are already using and it is already stored.
        if (normalized == baseUrl && serverStore.baseUrl != null) return normalized

        if (normalized != baseUrl) tokenStorage.clear()

        serverStore.baseUrl = normalized
        baseUrl = normalized
        api = PiroApiClient(baseUrl = normalized, tokens = tokenStorage)
        return normalized
    }
}
