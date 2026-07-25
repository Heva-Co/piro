package co.heva.piro.android.push

import android.os.Build
import co.heva.piro.shared.api.PiroApiClient
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.tasks.await

/**
 * Obtains this device's FCM registration token and associates it with the signed-in user via
 * POST /api/v1/devices. Called after login and whenever FCM rotates the token
 * (see [PiroMessagingService.onNewToken]). Idempotent on the backend — safe to call repeatedly.
 */
class DeviceRegistrar(private val api: PiroApiClient) {

    suspend fun registerCurrentDevice() {
        val token = FirebaseMessaging.getInstance().token.await()
        api.registerDevice(
            platform = "Android",
            token = token,
            deviceName = "${Build.MANUFACTURER} ${Build.MODEL}".trim(),
        )
    }
}
