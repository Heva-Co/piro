package co.heva.piro.android.alert

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import co.heva.piro.shared.api.PiroApiClient
import co.heva.piro.shared.model.AlertDetail
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class AlertsUiState(
    val loading: Boolean = true,
    val alerts: List<AlertDetail> = emptyList(),
    val error: String? = null,
)

/** Loads the active alerts list for the Alerts tab. */
class AlertsViewModel(private val api: PiroApiClient) : ViewModel() {

    private val _state = MutableStateFlow(AlertsUiState())
    val state: StateFlow<AlertsUiState> = _state.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            _state.update { it.copy(loading = true, error = null) }
            try {
                _state.update { it.copy(loading = false, alerts = api.getAlerts()) }
            } catch (e: Exception) {
                _state.update { it.copy(loading = false, error = "Could not load alerts.") }
            }
        }
    }
}
