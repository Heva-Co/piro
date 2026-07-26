package co.heva.piro.android.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import co.heva.piro.android.push.DeviceRegistrar
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.api.PiroApiException
import co.heva.piro.shared.auth.TokenStorage
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
    private val api: PiroApiClient,
    private val tokens: TokenStorage,
    private val deviceRegistrar: DeviceRegistrar,
) : ViewModel() {

    private val _state = MutableStateFlow(LoginUiState())
    val state: StateFlow<LoginUiState> = _state.asStateFlow()

    init {
        restoreSession()
        loadSsoConfig()
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
            _state.value = LoginUiState() // back to a fresh login screen
            loadSsoConfig()
        }
    }
}
