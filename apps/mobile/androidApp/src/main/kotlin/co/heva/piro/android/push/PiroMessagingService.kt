package co.heva.piro.android.push

import android.Manifest
import android.app.Notification
import android.app.NotificationManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import co.heva.piro.android.MainActivity
import co.heva.piro.android.PiroApp
import co.heva.piro.android.alert.IncomingAlertActivity
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlin.random.Random

/**
 * Receives FCM pushes and posts an on-call notification. A critical page goes to the
 * [NotificationChannels.CRITICAL] channel (alarm sound, DND bypass) so it breaks through a silenced
 * phone. The eventKey/url the backend sends as data lets the app open the right alert on tap.
 */
class PiroMessagingService : FirebaseMessagingService() {

    private companion object {
        const val TAG = "PiroMessaging"
    }

    override fun onNewToken(token: String) {
        // FCM rotated the token. If a session exists, push the new token to the backend right away so a
        // rotated token never leaves the backend paging a dead handle (which would fail and be pruned).
        val services = (application as? PiroApp)?.services ?: return
        if (services.tokenStorage.accessToken == null) return
        CoroutineScope(Dispatchers.IO).launch {
            runCatching { DeviceRegistrar(services.api, applicationContext).registerCurrentDevice() }
        }
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val data = message.data

        // A sealed push carries only "ciphertext" — the title, body, event key and alert id all live
        // inside it, so nothing readable crosses FCM or the Heva relay. A device that registered before
        // it published a public key still gets the legacy cleartext fields.
        val payload = data["ciphertext"]?.let { envelope ->
            runCatching { PushPayloadUnsealer.unseal(this, envelope) }
                .onFailure { Log.w(TAG, "Could not open a sealed push; dropping it.", it) }
                .getOrNull() ?: return
        } ?: PushPayload(
            title = data["title"] ?: "Piro alert",
            body = data["body"] ?: "",
            eventKey = data["eventKey"] ?: "",
            alertId = data["alertId"]?.toIntOrNull() ?: 0,
            url = data["url"],
        )

        val title = payload.title.ifEmpty { "Piro alert" }
        val body = payload.body
        val alertId = payload.alertId

        // "created" pages are the ones that must break through; recoveries/acks are informational.
        val isCritical = payload.eventKey.endsWith(":created")

        NotificationChannels.ensureCreated(this)

        if (isCritical) {
            // Hand off to the foreground service: it rings the alarm and launches the full-screen,
            // call-style page — which works from the background even on OEMs (Xiaomi/MIUI) that block
            // background activity starts from a plain notification.
            IncomingAlertService.start(this, alertId, title, body)
        } else {
            postInformationalNotification(title, body)
        }
    }

    /** A quiet notification for non-critical updates (acks, recoveries) — no alarm, no full screen. */
    private fun postInformationalNotification(title: String, body: String) {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
            return
        }
        val notification = NotificationCompat.Builder(this, NotificationChannels.DEFAULT)
            .setSmallIcon(android.R.drawable.stat_notify_error)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .build()
        NotificationManagerCompat.from(this).notify(Random.nextInt(), notification)
    }
}
