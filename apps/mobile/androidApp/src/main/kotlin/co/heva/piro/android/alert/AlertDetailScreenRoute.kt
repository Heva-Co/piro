package co.heva.piro.android.alert

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import co.heva.piro.android.PiroApp

/**
 * Route wrapper that owns the [AlertDetailViewModel] for a given alert and renders [AlertDetailScreen].
 * Keyed by alert id so navigating between alerts loads the right one.
 */
@Composable
fun AlertDetailScreenRoute(alertId: Int, onBack: () -> Unit) {
    val app = LocalContext.current.applicationContext as PiroApp
    val vm: AlertDetailViewModel = viewModel(
        key = "alert-$alertId",
        factory = AlertDetailViewModelFactory(app.services.api, alertId),
    )
    val state by vm.state.collectAsStateWithLifecycle()
    AlertDetailScreen(state = state, onAcknowledge = vm::acknowledge, onBack = onBack)
}
