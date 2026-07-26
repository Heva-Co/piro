package co.heva.piro.android.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import co.heva.piro.shared.api.PiroApiClient

/** Supplies [ProfileViewModel] its API client. */
class ProfileViewModelFactory(private val api: PiroApiClient) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = ProfileViewModel(api) as T
}
