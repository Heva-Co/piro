package co.heva.piro.android.alert

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import co.heva.piro.shared.api.PiroApiClient

/** Supplies [AlertsViewModel] its API client. */
class AlertsViewModelFactory(private val api: PiroApiClient) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = AlertsViewModel(api) as T
}
