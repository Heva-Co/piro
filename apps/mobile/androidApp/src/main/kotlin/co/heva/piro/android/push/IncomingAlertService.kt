package co.heva.piro.android.push

import android.app.Service
import android.content.Intent
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationManagerCompat
import co.heva.piro.android.alert.IncomingAlertActivity

/**
 * A short-lived foreground service that presents a critical page. Running as a foreground service is
 * what lets the app launch its full-screen [IncomingAlertActivity] from the background on OEMs (notably
 * Xiaomi/MIUI) that otherwise block background activity starts — a plain notification full-screen intent
 * is silently suppressed there. It also owns the ringing alarm, so audio starts the instant the page
 * arrives, and stops when the page is acknowledged/opened.
 */
class IncomingAlertService : Service() {

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val alertId = intent?.getIntExtra(EXTRA_ALERT_ID, 0) ?: 0
        val title = intent?.getStringExtra(EXTRA_TITLE) ?: "Critical alert"
        val body = intent?.getStringExtra(EXTRA_BODY) ?: ""

        NotificationChannels.ensureCreated(this)

        // Elevate to foreground with the page notification (also the fallback if the full-screen launch
        // is blocked). The notification carries the Acknowledge action and full-screen intent.
        val notificationId = if (alertId > 0) alertId else startId
        val notification = PageNotificationBuilder.build(this, alertId, title, body, notificationId)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                notificationId,
                notification,
                android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE,
            )
        } else {
            startForeground(notificationId, notification)
        }

        AlarmPlayer.start(applicationContext)

        // Launch the full-screen call-style page. From a foreground service this is allowed to start in
        // the background.
        startActivity(
            Intent(this, IncomingAlertActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK)
                putExtra(IncomingAlertActivity.EXTRA_ALERT_ID, alertId)
                putExtra(IncomingAlertActivity.EXTRA_TITLE, title)
                putExtra(IncomingAlertActivity.EXTRA_BODY, body)
            },
        )

        // The activity now owns the interaction; the service has done its job of getting it on screen.
        // Keep the notification posted (drop foreground without removing it) so it remains in the shade.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            stopForeground(STOP_FOREGROUND_DETACH)
        }
        return START_NOT_STICKY
    }

    companion object {
        const val EXTRA_ALERT_ID = "alertId"
        const val EXTRA_TITLE = "title"
        const val EXTRA_BODY = "body"

        fun start(context: android.content.Context, alertId: Int, title: String, body: String) {
            val intent = Intent(context, IncomingAlertService::class.java).apply {
                putExtra(EXTRA_ALERT_ID, alertId)
                putExtra(EXTRA_TITLE, title)
                putExtra(EXTRA_BODY, body)
            }
            NotificationManagerCompat.from(context) // ensure manager touched
            androidx.core.content.ContextCompat.startForegroundService(context, intent)
        }
    }
}
