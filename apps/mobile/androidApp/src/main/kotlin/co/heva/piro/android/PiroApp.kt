package co.heva.piro.android

import android.app.Application
import co.heva.piro.android.push.NotificationChannels

/**
 * Application entry point. Creates the notification channels once at startup (so a page can post to the
 * critical channel even if the app was launched straight into the background by FCM) and holds the
 * app-wide [ServiceLocator].
 */
class PiroApp : Application() {

    lateinit var services: ServiceLocator
        private set

    override fun onCreate() {
        super.onCreate()
        NotificationChannels.ensureCreated(this)
        services = ServiceLocator(this)
    }
}
