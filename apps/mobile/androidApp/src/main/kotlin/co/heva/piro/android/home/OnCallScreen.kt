package co.heva.piro.android.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import co.heva.piro.android.ui.PiroFlame
import co.heva.piro.android.ui.theme.PiroColors

/**
 * The On-call home: Piro-branded status card confirming the user is on call and this device is armed to
 * receive critical pages. Replaces the earlier plain HomeScreen with the flame mark and brand palette.
 */
@Composable
fun OnCallScreen(userName: String, notificationsGranted: Boolean, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        PiroFlame(size = 64.dp, color = PiroColors.Blue)

        Text(
            "You're on call",
            style = MaterialTheme.typography.headlineMedium,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(top = 20.dp),
        )
        Text(
            "Signed in as $userName",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 6.dp),
        )

        Surface(
            color = if (notificationsGranted) PiroColors.Up.copy(alpha = 0.12f) else PiroColors.Down.copy(alpha = 0.12f),
            shape = RoundedCornerShape(10.dp),
            modifier = Modifier.fillMaxWidth().padding(top = 28.dp),
        ) {
            Text(
                if (notificationsGranted) "This device will receive critical pages, even on silent."
                else "Enable notifications so pages can reach you.",
                style = MaterialTheme.typography.bodyMedium,
                color = if (notificationsGranted) PiroColors.Up else PiroColors.Down,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(16.dp).fillMaxWidth(),
            )
        }
    }
}
