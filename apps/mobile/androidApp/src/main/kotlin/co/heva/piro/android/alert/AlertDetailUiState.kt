package co.heva.piro.android.alert

import co.heva.piro.shared.model.AlertDetail

/** Snapshot the alert detail screen renders. */
data class AlertDetailUiState(
    val loading: Boolean = true,
    val alert: AlertDetail? = null,
    val error: String? = null,
    val acknowledging: Boolean = false,
)
