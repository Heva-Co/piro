package co.heva.piro.android.main

import androidx.activity.compose.BackHandler
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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import co.heva.piro.android.alert.AlertDetailScreenRoute
import co.heva.piro.android.alert.AlertsScreen
import co.heva.piro.android.home.OnCallScreen
import co.heva.piro.android.profile.ProfileScreen
import co.heva.piro.android.push.PushReadiness
import co.heva.piro.android.schedule.ScheduleScreen
import co.heva.piro.android.ui.theme.PiroColors

/**
 * The signed-in shell: a Piro-branded bottom navigation across On-call, Alerts, Schedule and Profile.
 * The alert detail is pushed over the Alerts tab (not as a global screen), so Back from a detail returns
 * to the alert list with the Alerts tab still selected — never resetting to another tab.
 *
 * [deepLinkAlertId] is set when a push notification is opened (piro://alert/{id}); the scaffold jumps to
 * the Alerts tab and shows that alert, then calls [onDeepLinkConsumed].
 */
@Composable
fun MainScaffold(
    userName: String,
    readiness: PushReadiness,
    deepLinkAlertId: Int?,
    onDeepLinkConsumed: () -> Unit,
    onSignOut: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var selected by remember { mutableStateOf(MainTab.OnCall) }
    // The alert currently open on top of the Alerts tab (null = showing the list).
    var openAlertId by remember { mutableStateOf<Int?>(null) }

    // A deep-linked page opens its alert on the Alerts tab.
    LaunchedEffect(deepLinkAlertId) {
        deepLinkAlertId?.let {
            selected = MainTab.Alerts
            openAlertId = it
            onDeepLinkConsumed()
        }
    }

    Scaffold(
        modifier = modifier,
        containerColor = MaterialTheme.colorScheme.background,
        bottomBar = {
            NavigationBar(containerColor = MaterialTheme.colorScheme.surface) {
                MainTab.entries.forEach { tab ->
                    NavigationBarItem(
                        selected = selected == tab,
                        onClick = {
                            // Re-tapping Alerts (or switching tabs) closes an open detail.
                            if (tab == MainTab.Alerts) openAlertId = null
                            selected = tab
                        },
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
                MainTab.OnCall -> OnCallScreen(userName = userName, readiness = readiness)
                MainTab.Alerts -> {
                    val alertId = openAlertId
                    if (alertId == null) {
                        AlertsScreen(onOpenAlert = { openAlertId = it })
                    } else {
                        // System Back returns to the list, keeping the Alerts tab selected.
                        BackHandler { openAlertId = null }
                        AlertDetailScreenRoute(alertId = alertId, onBack = { openAlertId = null })
                    }
                }
                MainTab.Schedule -> ScheduleScreen()
                MainTab.Profile -> ProfileScreen(onSignOut = onSignOut)
            }
        }
    }
}
