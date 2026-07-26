package co.heva.piro.android.push

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Whether this device will actually receive on-call pages — the honest state the On-call screen shows,
 * rather than just "was notification permission granted?". A device is only truly armed once its FCM
 * token is registered with the backend; if permission is missing, or the token/registration fails, the
 * UI must not promise pages it can't deliver. Mirrors the iOS `PushReadiness`.
 */
enum class PushReadiness {
    NeedsPermission, // the user hasn't allowed notifications
    Registering,     // permission granted; obtaining the FCM token / registering with the backend
    Registered,      // token registered with the backend — pages will arrive
    Failed,          // permission granted but registration didn't complete — pages won't arrive
}

/** App-wide observable push readiness, updated by [DeviceRegistrar] as registration progresses. */
object PushReadinessState {
    private val _state = MutableStateFlow(PushReadiness.NeedsPermission)
    val state: StateFlow<PushReadiness> = _state.asStateFlow()

    fun set(value: PushReadiness) { _state.value = value }

    /** Called when the OS notification permission result is known, before registration runs. */
    fun onPermissionResult(granted: Boolean) {
        _state.value = if (granted) PushReadiness.Registering else PushReadiness.NeedsPermission
    }
}
