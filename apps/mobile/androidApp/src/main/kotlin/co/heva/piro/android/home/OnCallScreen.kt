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
import co.heva.piro.android.push.PushReadiness
import co.heva.piro.android.ui.PiroFlame
import co.heva.piro.android.ui.theme.PiroColors

/**
 * The On-call home: Piro-branded status card that honestly reflects whether this device will receive
 * pages — driven by real [PushReadiness] (permission + FCM token + backend registration), not just the
 * notification permission. Mirrors the iOS OnCallView.
 */
@Composable
fun OnCallScreen(userName: String, readiness: PushReadiness, modifier: Modifier = Modifier) {
    Column(modifier = modifier.fillMaxSize().padding(20.dp)) {
        Text(
            "On-call",
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground,
        )
        Column(
            modifier = Modifier.fillMaxSize().padding(horizontal = 4.dp),
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

        val (text, color) = bannerFor(readiness)
        Surface(
            color = color.copy(alpha = 0.12f),
            shape = RoundedCornerShape(10.dp),
            modifier = Modifier.fillMaxWidth().padding(top = 28.dp),
        ) {
            Text(
                text,
                style = MaterialTheme.typography.bodyMedium,
                color = color,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(16.dp).fillMaxWidth(),
            )
        }
        }
    }
}

/** The honest banner message + color for each readiness state — only "registered" promises pages. */
private fun bannerFor(readiness: PushReadiness): Pair<String, androidx.compose.ui.graphics.Color> = when (readiness) {
    PushReadiness.Registered -> "This device will receive critical pages, even on silent." to PiroColors.Up
    PushReadiness.Registering -> "Arming this device to receive pages…" to PiroColors.Degraded
    PushReadiness.NeedsPermission -> "Enable notifications so pages can reach you." to PiroColors.Down
    PushReadiness.Failed -> "This device isn't registered for pages yet — you may not be paged." to PiroColors.Degraded
}
