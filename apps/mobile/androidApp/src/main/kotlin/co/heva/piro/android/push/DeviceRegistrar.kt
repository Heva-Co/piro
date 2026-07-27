package co.heva.piro.android.push

import android.content.Context
import android.os.Build
import co.heva.piro.shared.api.PiroApiClient
import com.google.firebase.messaging.FirebaseMessaging
import kotlinx.coroutines.tasks.await

/**
 * Obtains this device's FCM registration token and associates it with the signed-in user via
 * POST /api/v1/devices. Called after login and whenever FCM rotates the token
 * (see [PiroMessagingService.onNewToken]). Idempotent on the backend — safe to call repeatedly.
 */
class DeviceRegistrar(private val api: PiroApiClient, private val context: Context) {

    suspend fun registerCurrentDevice() {
        try {
            val token = FirebaseMessaging.getInstance().token.await()
            api.registerDevice(
                platform = "Android",
                token = token,
                deviceName = "${Build.MANUFACTURER} ${Build.MODEL}".trim(),
                // Generated on first call and persisted; the private half never leaves the device.
                pushPublicKey = PushKeyStore.publicKeyBase64Url(context),
            )
            // Only now is the device truly armed: FCM gave a token AND the backend accepted it.
            PushReadinessState.set(PushReadiness.Registered)
        } catch (e: Exception) {
            // No FCM token (e.g. no Google Play Services) or the backend rejected it — don't promise pages.
            PushReadinessState.set(PushReadiness.Failed)
            throw e
        }
    }
}
