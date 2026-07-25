package co.heva.piro.android.alert

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import co.heva.piro.shared.model.AlertDetail

/**
 * The alert detail screen a page opens into: severity banner, service/check/message and metadata, and
 * an Acknowledge button that pauses escalation. Stateless — the host wires callbacks to the ViewModel.
 */
@Composable
fun AlertDetailScreen(
    state: AlertDetailUiState,
    onAcknowledge: () -> Unit,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        when {
            state.loading -> {
                Row(Modifier.fillMaxWidth().padding(top = 48.dp), horizontalArrangement = Arrangement.Center) {
                    CircularProgressIndicator()
                }
            }
            state.alert != null -> AlertContent(state.alert, state, onAcknowledge)
            else -> Text(state.error ?: "Alert not found.", color = MaterialTheme.colorScheme.error)
        }

        Text(
            "← Back",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(top = 8.dp).fillMaxWidth().clickable(onClick = onBack),
        )
    }
}

@Composable
private fun AlertContent(alert: AlertDetail, state: AlertDetailUiState, onAcknowledge: () -> Unit) {
    val severity = alert.severity ?: "Alert"
    val isCritical = severity.equals("Critical", ignoreCase = true)
    val bannerColor = when {
        alert.isResolved -> Color(0xFF16A34A)
        isCritical -> Color(0xFFDC2626)
        else -> Color(0xFFD97706)
    }

    Surface(color = bannerColor, shape = RoundedCornerShape(12.dp), modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp)) {
            Text(
                if (alert.isResolved) "RESOLVED" else severity.uppercase(),
                color = Color.White,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Bold,
            )
            Text(
                "${alert.checkName ?: "Check"} on ${alert.serviceName ?: "service"}",
                color = Color.White,
                style = MaterialTheme.typography.titleLarge,
            )
        }
    }

    alert.message?.let { Field("Message", it) }
    alert.impactAtFireTime?.let { Field("Impact", it) }
    alert.firedAt?.let { Field("Fired at", co.heva.piro.android.util.DateFormat.localDateTime(it)) }
    Field("Occurrences", alert.occurrenceCount.toString())

    when {
        alert.isAcknowledged -> Field("Acknowledged", "by ${alert.acknowledgedBy ?: "someone"}")
        alert.escalationExhaustedAt != null -> Field("Escalation", "Halted — acknowledge to resume")
    }

    if (!alert.isAcknowledged && !alert.isResolved) {
        Button(
            onClick = onAcknowledge,
            enabled = !state.acknowledging,
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) {
            Text(if (state.acknowledging) "Acknowledging…" else "Acknowledge")
        }
    }

    state.error?.let {
        Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun Field(label: String, value: String) {
    Column(Modifier.fillMaxWidth()) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.bodyLarge)
    }
}
