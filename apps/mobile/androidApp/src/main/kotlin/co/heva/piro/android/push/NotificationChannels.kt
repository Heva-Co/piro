package co.heva.piro.android.push

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.media.AudioAttributes
import android.media.RingtoneManager
import android.os.Build

/**
 * Defines the app's notification channels. The critical channel is the whole point of an on-call app:
 * it is created at max importance with an alarm-usage sound and, on supported OS versions, is allowed
 * to bypass Do Not Disturb — so a page still rings when the phone is silenced. The user can still
 * revoke DND bypass in system settings, but the app requests it by default.
 */
object NotificationChannels {

    const val CRITICAL = "piro_critical"
    const val DEFAULT = "piro_default"

    fun ensureCreated(context: Context) {
        val manager = context.getSystemService(NotificationManager::class.java) ?: return

        val alarmSound = RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)
        val audioAttributes = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_ALARM)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()

        val critical = NotificationChannel(
            CRITICAL,
            "Critical pages",
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = "On-call pages that must break through silent / Do Not Disturb."
            setSound(alarmSound, audioAttributes)
            enableVibration(true)
            vibrationPattern = longArrayOf(0, 500, 250, 500, 250, 500)
            setBypassDnd(true)
            lockscreenVisibility = android.app.Notification.VISIBILITY_PUBLIC
        }

        val default = NotificationChannel(
            DEFAULT,
            "Updates",
            NotificationManager.IMPORTANCE_DEFAULT,
        ).apply {
            description = "Acknowledgements, recoveries and other non-critical updates."
        }

        manager.createNotificationChannel(critical)
        manager.createNotificationChannel(default)
    }
}
