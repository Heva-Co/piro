package co.heva.piro.android.main

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.NotificationsActive
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.ui.graphics.vector.ImageVector

/** The bottom-navigation destinations, adapted from the mockup to the Piro on-call domain. */
enum class MainTab(val label: String, val icon: ImageVector) {
    OnCall("On-call", Icons.Outlined.Shield),
    Alerts("Alerts", Icons.Outlined.NotificationsActive),
    Schedule("Schedule", Icons.Outlined.CalendarMonth),
    Settings("Settings", Icons.Outlined.Settings),
}
