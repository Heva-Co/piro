package co.heva.piro.shared.auth

/**
 * Secure, platform-backed storage for the auth tokens. Android backs this with the Keystore-encrypted
 * shared preferences; iOS (Phase 3) with the Keychain. The shared code only ever sees this interface,
 * so token handling stays identical across platforms.
 */
interface TokenStorage {
    var accessToken: String?
    var refreshToken: String?

    fun clear()
}
