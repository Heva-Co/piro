package co.heva.piro.android.alert

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import co.heva.piro.shared.api.PiroApiClient
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/** Loads one alert's detail and acknowledges it. */
class AlertDetailViewModel(
    private val api: PiroApiClient,
    private val alertId: Int,
) : ViewModel() {

    private val _state = MutableStateFlow(AlertDetailUiState())
    val state: StateFlow<AlertDetailUiState> = _state.asStateFlow()

    init {
        load()
    }

    fun load() {
        viewModelScope.launch {
            _state.update { it.copy(loading = true, error = null) }
            try {
                val alert = api.getAlert(alertId)
                _state.update { it.copy(loading = false, alert = alert) }
            } catch (e: Exception) {
                _state.update { it.copy(loading = false, error = "Could not load the alert.") }
            }
        }
    }

    fun acknowledge() {
        if (_state.value.acknowledging) return
        viewModelScope.launch {
            _state.update { it.copy(acknowledging = true, error = null) }
            try {
                val updated = api.acknowledgeAlert(alertId)
                _state.update { it.copy(acknowledging = false, alert = updated) }
            } catch (e: Exception) {
                _state.update { it.copy(acknowledging = false, error = "Acknowledge failed.") }
            }
        }
    }
}
