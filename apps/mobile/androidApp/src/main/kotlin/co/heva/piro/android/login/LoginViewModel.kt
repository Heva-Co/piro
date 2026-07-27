package co.heva.piro.android.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import android.content.Context
import co.heva.piro.android.ServiceLocator
import co.heva.piro.android.push.DeviceRegistrar
import co.heva.piro.shared.api.PiroApiException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * Drives the login screen: restores an existing session on launch, loads SSO configuration, signs in by
 * email/password or completes an SSO callback, and on success registers this device's push token so the
 * backend can page it.
 */
class LoginViewModel(
    private val services: ServiceLocator,
    private val appContext: Context,
) : ViewModel() {

    // Read through the locator on every use: pointing the app at another server replaces the client.
    private val api get() = services.api
    private val tokens get() = services.tokenStorage
    private val deviceRegistrar get() = DeviceRegistrar(services.api, appContext)

    private val _state = MutableStateFlow(LoginUiState(serverUrl = services.baseUrl))
    val state: StateFlow<LoginUiState> = _state.asStateFlow()

    init {
        restoreSession()
        loadSsoConfig()
    }

    fun onServerUrlChange(value: String) =
        _state.update { it.copy(serverUrl = value, serverError = null, error = null) }

    /**
     * Points the app at the typed server, then reloads its SSO configuration — which providers exist is a
     * property of the server, so it has to be re-read whenever the server changes.
     *
     * Returns false when the URL is unusable, so the caller can stop before attempting a sign-in.
     */
    fun applyServer(): Boolean {
        val applied = services.useServer(_state.value.serverUrl)
        if (applied == null) {
            _state.update {
                it.copy(serverError = "Enter a valid server address, e.g. https://piro.example.com")
            }
            return false
        }

        _state.update { it.copy(serverUrl = applied, serverError = null) }
        loadSsoConfig()
        return true
    }

    /**
     * If a token is already stored, validate it with /me and go straight to the signed-in state — the
     * user shouldn't have to log in again every time the app reopens. Also re-registers the device so a
     * rotated FCM token gets refreshed on the backend. A failed/expired token falls through to login.
     */
    private fun restoreSession() {
        if (tokens.accessToken == null) return
        viewModelScope.launch {
            _state.update { it.copy(isSubmitting = true) }
            try {
                val profile = api.me() // refreshes the access token on a 401 via the client
                runCatching { deviceRegistrar.registerCurrentDevice() }
                _state.update { it.copy(isSubmitting = false, signedIn = true, email = profile.email) }
            } catch (e: Exception) {
                tokens.clear()
                _state.update { it.copy(isSubmitting = false) }
            }
        }
    }

    fun onEmailChange(value: String) = _state.update { it.copy(email = value, error = null) }
    fun onPasswordChange(value: String) = _state.update { it.copy(password = value, error = null) }

    private fun loadSsoConfig() {
        viewModelScope.launch {
            runCatching {
                val mode = api.getSsoMode()
                val providers = api.getOidcProviders()
                _state.update { it.copy(ssoOnly = mode.ssoOnly, providers = providers) }
            }
            // A missing SSO endpoint is non-fatal: fall back to password login.
        }
    }

    fun signIn() {
        val current = _state.value
        if (current.isSubmitting) return
        // Apply the typed server before authenticating, or the credentials go to whatever host was
        // configured previously.
        if (!applyServer()) return
        if (current.email.isBlank() || current.password.isBlank()) {
            _state.update { it.copy(error = "Enter your email and password.") }
            return
        }
        viewModelScope.launch {
            _state.update { it.copy(isSubmitting = true, error = null) }
            try {
                api.signIn(current.email.trim(), current.password)
                afterSignedIn()
            } catch (e: PiroApiException) {
                _state.update { it.copy(isSubmitting = false, error = e.message ?: "Sign-in failed.") }
            } catch (e: Exception) {
                _state.update { it.copy(isSubmitting = false, error = "Could not reach the server.") }
            }
        }
    }

    /** Called by MainActivity when the SSO browser redirect delivers a code+state. */
    fun completeSso(code: String, state: String) {
        viewModelScope.launch {
            _state.update { it.copy(isSubmitting = true, error = null) }
            try {
                api.completeOidcCallback(code, state)
                afterSignedIn()
            } catch (e: Exception) {
                _state.update { it.copy(isSubmitting = false, error = "SSO sign-in failed.") }
            }
        }
    }

    private suspend fun afterSignedIn() {
        // Best-effort: registration failing (e.g. no FCM token yet) must not block a successful login.
        runCatching { deviceRegistrar.registerCurrentDevice() }
        _state.update { it.copy(isSubmitting = false, signedIn = true) }
    }

    fun signOut() {
        viewModelScope.launch {
            runCatching { api.signOut() }
            // Fresh login screen, but keep pointing at the same server — signing out is not a reason to
            // make the user retype their host.
            _state.value = LoginUiState(serverUrl = services.baseUrl)
            loadSsoConfig()
        }
    }
}
