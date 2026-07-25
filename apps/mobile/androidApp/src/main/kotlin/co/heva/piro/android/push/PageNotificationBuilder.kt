package co.heva.piro.android.push

import android.app.Notification
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.core.app.NotificationCompat
import co.heva.piro.android.MainActivity
import co.heva.piro.android.alert.IncomingAlertActivity

/**
 * Builds the page notification shared by the messaging service and the foreground service: critical
 * channel, Acknowledge action, tap-opens-detail, and a full-screen intent to the call-style page.
 */
object PageNotificationBuilder {

    fun build(context: Context, alertId: Int, title: String, body: String, notificationId: Int): Notification {
        val contentIntent = PendingIntent.getActivity(
            context,
            notificationId,
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
                if (alertId > 0) data = Uri.parse("piro://alert/$alertId")
            },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        val fullScreenIntent = PendingIntent.getActivity(
            context,
            notificationId + 100_000,
            Intent(context, IncomingAlertActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
                putExtra(IncomingAlertActivity.EXTRA_ALERT_ID, alertId)
                putExtra(IncomingAlertActivity.EXTRA_TITLE, title)
                putExtra(IncomingAlertActivity.EXTRA_BODY, body)
            },
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        val builder = NotificationCompat.Builder(context, NotificationChannels.CRITICAL)
            .setSmallIcon(android.R.drawable.stat_notify_error)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setOngoing(true)
            .setContentIntent(contentIntent)
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setCategory(NotificationCompat.CATEGORY_CALL)
            .setVisibility(Notification.VISIBILITY_PUBLIC)
            .setFullScreenIntent(fullScreenIntent, true)

        if (alertId > 0) {
            val ackIntent = PendingIntent.getBroadcast(
                context,
                notificationId,
                Intent(context, AckReceiver::class.java).apply {
                    action = AckReceiver.ACTION_ACK
                    putExtra(AckReceiver.EXTRA_ALERT_ID, alertId)
                    putExtra(AckReceiver.EXTRA_NOTIFICATION_ID, notificationId)
                },
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
            )
            builder.addAction(0, "Acknowledge", ackIntent)
        }

        return builder.build()
    }
}
