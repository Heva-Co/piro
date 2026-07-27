package co.heva.piro.android

import android.content.Context

/**
 * Persists the Piro API base URL the user points the app at. Piro is self-hosted, so there is no single
 * server — each user enters their own on the login screen, and it's remembered across launches. Stored
 * in plain SharedPreferences because it's configuration, not a secret; tokens live in
 * [co.heva.piro.shared.auth.EncryptedTokenStorage].
 *
 * Mirrors the iOS `ServerStore`, including the normalization rules, so the two apps accept exactly the
 * same input.
 */
class ServerStore(context: Context) {

    private val prefs = context.applicationContext
        .getSharedPreferences("piro_server", Context.MODE_PRIVATE)

    /** The stored base URL, or null when the user has not chosen one yet. */
    var baseUrl: String?
        get() = prefs.getString(KEY, null)
        set(value) {
            prefs.edit().apply {
                if (value == null) remove(KEY) else putString(KEY, value)
            }.apply()
        }

    companion object {
        private const val KEY = "piro.api.baseURL"

        /**
         * Normalizes user input into a usable base URL: trims whitespace, defaults a missing scheme to
         * https, and strips trailing slashes. Returns null when the result is not a valid http(s) URL
         * with a host, so the UI can ask for a correction instead of failing later on every request.
         */
        fun normalize(raw: String): String? {
            var s = raw.trim()
            if (s.isEmpty()) return null

            val lower = s.lowercase()
            if (!lower.startsWith("http://") && !lower.startsWith("https://")) {
                s = "https://$s"
            }
            while (s.endsWith("/")) s = s.dropLast(1)

            // android.net.Uri never throws, so validate the parts we actually depend on.
            val uri = runCatching { android.net.Uri.parse(s) }.getOrNull() ?: return null
            val scheme = uri.scheme?.lowercase()
            if (scheme != "http" && scheme != "https") return null
            if (uri.host.isNullOrEmpty()) return null

            return s
        }
    }
}
