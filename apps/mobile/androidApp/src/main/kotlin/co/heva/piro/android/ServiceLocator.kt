package co.heva.piro.android

import android.content.Context
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.auth.EncryptedTokenStorage
import co.heva.piro.shared.auth.TokenStorage

/**
 * Minimal hand-rolled dependency container — the app is small enough that a DI framework would be
 * overkill. Holds the single [TokenStorage] and [PiroApiClient] used everywhere.
 */
class ServiceLocator(context: Context) {
    val tokenStorage: TokenStorage = EncryptedTokenStorage(context.applicationContext)

    val api: PiroApiClient = PiroApiClient(
        baseUrl = BuildConfig.PIRO_API_BASE_URL,
        tokens = tokenStorage,
    )
}
