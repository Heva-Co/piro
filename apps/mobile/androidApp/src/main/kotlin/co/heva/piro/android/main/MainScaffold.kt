package co.heva.piro.android.main

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import co.heva.piro.android.alert.AlertsScreen
import co.heva.piro.android.home.OnCallScreen
import co.heva.piro.android.placeholder.PlaceholderScreen
import co.heva.piro.android.ui.theme.PiroColors

/**
 * The signed-in shell: a Piro-branded bottom navigation across On-call, Alerts, Schedule and Settings.
 * On-call and Alerts are real; Schedule and Settings are branded placeholders until built.
 */
@Composable
fun MainScaffold(
    userName: String,
    notificationsGranted: Boolean,
    onOpenAlert: (Int) -> Unit,
    onSignOut: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var selected by remember { mutableStateOf(MainTab.OnCall) }

    Scaffold(
        modifier = modifier,
        containerColor = MaterialTheme.colorScheme.background,
        bottomBar = {
            NavigationBar(containerColor = MaterialTheme.colorScheme.surface) {
                MainTab.entries.forEach { tab ->
                    NavigationBarItem(
                        selected = selected == tab,
                        onClick = { selected = tab },
                        icon = { Icon(tab.icon, contentDescription = tab.label) },
                        label = { Text(tab.label) },
                        colors = NavigationBarItemDefaults.colors(
                            selectedIconColor = PiroColors.Blue,
                            selectedTextColor = PiroColors.Blue,
                            indicatorColor = MaterialTheme.colorScheme.surfaceVariant,
                        ),
                    )
                }
            }
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when (selected) {
                MainTab.OnCall -> OnCallScreen(userName = userName, notificationsGranted = notificationsGranted)
                MainTab.Alerts -> AlertsScreen(onOpenAlert = onOpenAlert)
                MainTab.Schedule -> PlaceholderScreen(
                    title = "Schedule",
                    message = "Your on-call rotation and shifts will appear here.",
                )
                MainTab.Settings -> PlaceholderScreen(
                    title = "Settings",
                    message = "Profile, notification preferences and sign out.",
                    actionLabel = "Sign out",
                    onAction = onSignOut,
                )
            }
        }
    }
}
