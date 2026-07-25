package co.heva.piro.android.login

import co.heva.piro.shared.model.OidcProvider

/** Immutable snapshot the login screen renders. */
data class LoginUiState(
    val email: String = "",
    val password: String = "",
    val isSubmitting: Boolean = false,
    val error: String? = null,
    val ssoOnly: Boolean = false,
    val providers: List<OidcProvider> = emptyList(),
    val signedIn: Boolean = false,
)
