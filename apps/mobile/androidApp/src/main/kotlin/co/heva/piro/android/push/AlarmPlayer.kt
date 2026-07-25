package co.heva.piro.android.push

import android.content.Context
import android.media.AudioAttributes
import android.media.MediaPlayer
import android.media.RingtoneManager

/**
 * Actively plays the alarm ringtone (looping, on the ALARM stream) when a critical page arrives. This is
 * the on-call app's guarantee that a page is heard: it does not rely on the notification channel's own
 * sound, which a strict Do Not Disturb ("total silence") mode can suppress even for alarm-usage channels.
 * Like PagerDuty, the page keeps ringing until the user acts (opens the alert / acknowledges), at which
 * point [stop] is called.
 */
object AlarmPlayer {
    private var player: MediaPlayer? = null

    @Synchronized
    fun start(context: Context) {
        if (player != null) return // already ringing
        val uri = RingtoneManager.getActualDefaultRingtoneUri(context, RingtoneManager.TYPE_ALARM)
            ?: RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)
        player = MediaPlayer().apply {
            setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_ALARM)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                    .build(),
            )
            setDataSource(context, uri)
            isLooping = true
            setOnPreparedListener { start() }
            prepareAsync()
        }
    }

    @Synchronized
    fun stop() {
        player?.let {
            runCatching { if (it.isPlaying) it.stop() }
            it.release()
        }
        player = null
    }
}
