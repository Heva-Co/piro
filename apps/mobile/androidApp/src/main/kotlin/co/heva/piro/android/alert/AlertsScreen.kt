package co.heva.piro.android.alert

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import co.heva.piro.android.PiroApp
import co.heva.piro.shared.model.AlertDetail
import co.heva.piro.android.ui.theme.PiroColors

/**
 * The Alerts tab: the current active alerts, styled after the mockup's agenda cards. Tapping a card
 * opens its detail. Empty state is a calm "all clear" rather than a blank screen.
 */
@Composable
fun AlertsScreen(onOpenAlert: (Int) -> Unit, modifier: Modifier = Modifier) {
    val app = androidx.compose.ui.platform.LocalContext.current.applicationContext as PiroApp
    val vm: AlertsViewModel = viewModel(factory = AlertsViewModelFactory(app.services.api))
    val state by vm.state.collectAsStateWithLifecycle()

    Column(modifier = modifier.fillMaxSize().padding(20.dp)) {
        Text(
            "Active alerts",
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground,
        )

        when {
            state.loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            state.error != null -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(state.error!!, color = MaterialTheme.colorScheme.error)
            }
            state.alerts.isEmpty() -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("All clear — no active alerts.", color = PiroColors.Up, fontWeight = FontWeight.Medium)
            }
            else -> LazyColumn(
                verticalArrangement = Arrangement.spacedBy(12.dp),
                modifier = Modifier.padding(top = 16.dp),
            ) {
                items(state.alerts, key = { it.id }) { alert ->
                    AlertCard(alert = alert, onClick = { onOpenAlert(alert.id) })
                }
            }
        }
    }
}

@Composable
private fun AlertCard(alert: AlertDetail, onClick: () -> Unit) {
    val dotColor = when {
        alert.isResolved -> PiroColors.Up
        alert.severity.equals("Critical", ignoreCase = true) -> PiroColors.Down
        else -> PiroColors.Degraded
    }
    Surface(
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(12.dp),
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
    ) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.size(10.dp).let { it }) {
                    Surface(color = dotColor, shape = CircleShape, modifier = Modifier.size(10.dp)) {}
                }
                Text(
                    "  ${(alert.severity ?: "Alert").uppercase()}",
                    style = MaterialTheme.typography.labelMedium,
                    color = dotColor,
                    fontWeight = FontWeight.Bold,
                )
                if (alert.isAcknowledged) {
                    Text(
                        "  • ACK",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Text(
                "${alert.checkName ?: "Check"} on ${alert.serviceName ?: "service"}",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.padding(top = 6.dp),
            )
            alert.message?.let {
                Text(
                    it,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
            Text(
                "View details →",
                style = MaterialTheme.typography.labelMedium,
                color = PiroColors.Blue,
                modifier = Modifier.padding(top = 10.dp),
            )
        }
    }
}
