package co.heva.piro.android.schedule

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import co.heva.piro.shared.api.PiroApiClient

/** Supplies [ScheduleViewModel] its API client. */
class ScheduleViewModelFactory(private val api: PiroApiClient) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T = ScheduleViewModel(api) as T
}
