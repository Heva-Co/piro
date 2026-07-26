package co.heva.piro.android.alert

import android.app.KeyguardManager
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import co.heva.piro.android.MainActivity
import co.heva.piro.android.PiroApp
import co.heva.piro.android.push.AlarmPlayer
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/**
 * Full-screen "incoming page" — modeled on an incoming phone call, not a notification. Launched by a
 * critical page's full-screen intent, it turns the screen on and shows over the lock screen with a big
 * red banner, the alert text, and large Acknowledge / View details actions, while the alarm rings. This
 * is the on-call experience: impossible to miss, actionable in one tap.
 */
class IncomingAlertActivity : ComponentActivity() {

    private val alertId: Int get() = intent.getIntExtra(EXTRA_ALERT_ID, 0)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        showOverLockScreen()

        val title = intent.getStringExtra(EXTRA_TITLE) ?: "Critical alert"
        val body = intent.getStringExtra(EXTRA_BODY) ?: ""

        setContent {
            co.heva.piro.android.ui.theme.PiroTheme(darkTheme = true) {
                IncomingAlertScreen(
                    title = title,
                    body = body,
                    onAcknowledge = { acknowledgeAndClose() },
                    onViewDetails = { openDetailsAndClose() },
                )
            }
        }
    }

    private fun showOverLockScreen() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
            (getSystemService(Context.KEYGUARD_SERVICE) as? KeyguardManager)?.requestDismissKeyguard(this, null)
        } else {
            @Suppress("DEPRECATION")
            window.addFlags(
                android.view.WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                    android.view.WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
                    android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON,
            )
        }
    }

    private fun acknowledgeAndClose() {
        AlarmPlayer.stop()
        val services = (application as? PiroApp)?.services
        val id = alertId
        if (services != null && id > 0) {
            CoroutineScope(Dispatchers.IO).launch {
                runCatching { services.api.acknowledgeAlert(id) }
            }
        }
        finish()
    }

    private fun openDetailsAndClose() {
        AlarmPlayer.stop()
        startActivity(
            Intent(this, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
                if (alertId > 0) data = android.net.Uri.parse("piro://alert/$alertId")
            },
        )
        finish()
    }

    companion object {
        const val EXTRA_ALERT_ID = "alertId"
        const val EXTRA_TITLE = "title"
        const val EXTRA_BODY = "body"
    }
}

@Composable
private fun IncomingAlertScreen(
    title: String,
    body: String,
    onAcknowledge: () -> Unit,
    onViewDetails: () -> Unit,
) {
    Surface(color = Color(0xFFB91C1C), modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier.fillMaxSize().padding(28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween,
        ) {
            Column(
                modifier = Modifier.fillMaxWidth().padding(top = 64.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                co.heva.piro.android.ui.PiroFlame(size = 48.dp, color = Color.White)
                Spacer(Modifier.height(12.dp))
                Text("PIRO ON-CALL", color = Color.White.copy(alpha = 0.8f), letterSpacing = 3.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(24.dp))
                Text(
                    title,
                    color = Color.White,
                    fontSize = 28.sp,
                    fontWeight = FontWeight.Bold,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(16.dp))
                Text(body, color = Color.White.copy(alpha = 0.9f), fontSize = 16.sp, textAlign = TextAlign.Center)
            }

            Column(modifier = Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Button(
                    onClick = onAcknowledge,
                    colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Color(0xFFB91C1C)),
                    modifier = Modifier.fillMaxWidth().height(56.dp),
                ) {
                    Text("Acknowledge", fontSize = 18.sp, fontWeight = FontWeight.Bold)
                }
                OutlinedButton(
                    onClick = onViewDetails,
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = Color.White),
                    modifier = Modifier.fillMaxWidth().height(56.dp),
                ) {
                    Text("View details", fontSize = 16.sp)
                }
            }
        }
    }
}
