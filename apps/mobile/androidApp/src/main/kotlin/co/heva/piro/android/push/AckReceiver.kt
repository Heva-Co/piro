package co.heva.piro.android.push

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationManagerCompat
import co.heva.piro.android.PiroApp
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/**
 * Handles the "Acknowledge" action on a page notification: acknowledges the alert on the backend
 * (pausing escalation) straight from the notification shade, stops the alarm, and dismisses the
 * notification — no need to open the app. Mirrors PagerDuty's quick-ack.
 */
class AckReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        val alertId = intent.getIntExtra(EXTRA_ALERT_ID, 0)
        val notificationId = intent.getIntExtra(EXTRA_NOTIFICATION_ID, 0)

        AlarmPlayer.stop()
        NotificationManagerCompat.from(context).cancel(notificationId)

        val services = (context.applicationContext as? PiroApp)?.services ?: return
        if (alertId <= 0 || services.tokenStorage.accessToken == null) return

        val pending = goAsync()
        CoroutineScope(Dispatchers.IO).launch {
            try {
                services.api.acknowledgeAlert(alertId)
            } catch (_: Exception) {
                // Best-effort; the user can still ack from the detail screen.
            } finally {
                pending.finish()
            }
        }
    }

    companion object {
        const val ACTION_ACK = "co.heva.piro.action.ACK"
        const val EXTRA_ALERT_ID = "alertId"
        const val EXTRA_NOTIFICATION_ID = "notificationId"
    }
}
