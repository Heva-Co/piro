package co.heva.piro.shared.auth

/**
 * iOS [TokenStorage]. Phase 3 backs this with the Keychain (Security framework); for now it is an
 * in-memory placeholder so the iOS target compiles alongside the shared code written in Phase 2.
 */
class KeychainTokenStorage : TokenStorage {
    override var accessToken: String? = null
    override var refreshToken: String? = null

    override fun clear() {
        accessToken = null
        refreshToken = null
    }
}
