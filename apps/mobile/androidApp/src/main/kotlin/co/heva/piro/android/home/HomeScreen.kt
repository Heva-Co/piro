package co.heva.piro.android.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

/**
 * Placeholder post-login screen for Phase 2 — confirms the device is associated and ready to receive
 * pages. Phase-2 scope stops here; the alert inbox / ack UI comes later.
 */
@Composable
fun HomeScreen(userName: String, notificationsGranted: Boolean, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text("You're on call", style = MaterialTheme.typography.headlineMedium)
        Text(
            "Signed in as $userName",
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.padding(top = 8.dp),
        )
        Text(
            if (notificationsGranted) "This device will receive critical pages."
            else "Enable notifications to receive pages.",
            style = MaterialTheme.typography.bodyMedium,
            color = if (notificationsGranted) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
            modifier = Modifier.padding(top = 16.dp),
        )
    }
}
