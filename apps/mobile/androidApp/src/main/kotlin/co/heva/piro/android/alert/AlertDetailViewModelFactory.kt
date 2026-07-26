package co.heva.piro.android.alert

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import co.heva.piro.shared.api.PiroApiClient

/** Supplies [AlertDetailViewModel] its API client and the alert id to load. */
class AlertDetailViewModelFactory(
    private val api: PiroApiClient,
    private val alertId: Int,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T =
        AlertDetailViewModel(api, alertId) as T
}
