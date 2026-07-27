package co.heva.piro.android.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import android.content.Context
import co.heva.piro.android.ServiceLocator

/**
 * Supplies the [LoginViewModel] its service locator and device registrar (no DI framework in use).
 *
 * The locator is passed rather than a bare API client: changing the server rebuilds that client, and a
 * captured instance would keep talking to the old host.
 */
class LoginViewModelFactory(
    private val services: ServiceLocator,
    private val appContext: Context,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T =
        LoginViewModel(services, appContext) as T
}
