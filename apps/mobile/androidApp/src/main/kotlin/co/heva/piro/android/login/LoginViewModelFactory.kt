package co.heva.piro.android.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import co.heva.piro.android.push.DeviceRegistrar
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.auth.TokenStorage

/** Supplies the [LoginViewModel] its API client, token storage, and device registrar (no DI framework in use). */
class LoginViewModelFactory(
    private val api: PiroApiClient,
    private val tokens: TokenStorage,
    private val deviceRegistrar: DeviceRegistrar,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T =
        LoginViewModel(api, tokens, deviceRegistrar) as T
}
