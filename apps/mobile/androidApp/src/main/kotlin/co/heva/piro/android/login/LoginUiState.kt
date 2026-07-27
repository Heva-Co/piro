package co.heva.piro.android.login

import co.heva.piro.shared.model.OidcProvider

/** Immutable snapshot the login screen renders. */
data class LoginUiState(
    /** The Piro server the app points at. Editable because Piro is self-hosted — there is no single host. */
    val serverUrl: String = "",
    val email: String = "",
    val password: String = "",
    val isSubmitting: Boolean = false,
    val error: String? = null,
    /** Set when the typed server URL is not a usable http(s) address, shown under that field. */
    val serverError: String? = null,
    val ssoOnly: Boolean = false,
    val providers: List<OidcProvider> = emptyList(),
    val signedIn: Boolean = false,
)
