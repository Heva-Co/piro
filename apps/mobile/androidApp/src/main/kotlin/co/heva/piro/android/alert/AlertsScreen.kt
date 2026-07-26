package co.heva.piro.android.alert

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CardDefaults
import co.heva.piro.android.ui.SkeletonList
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Size
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

    Column(modifier = modifier.fillMaxSize()) {
        Text(
            "Active alerts",
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(20.dp),
        )

        when {
            state.loading -> SkeletonList(Modifier.padding(horizontal = 20.dp), count = 5, lines = 3)
            state.error != null -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(state.error!!, color = MaterialTheme.colorScheme.error)
            }
            state.alerts.isEmpty() -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("All clear — no active alerts.", color = PiroColors.Up, fontWeight = FontWeight.Medium)
            }
            // contentPadding (not Modifier.padding) so the first/last card's elevation shadow has room
            // inside the scroll area and isn't clipped against the list's edges.
            else -> LazyColumn(
                verticalArrangement = Arrangement.spacedBy(12.dp),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 20.dp, vertical = 6.dp),
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
    ElevatedCard(
        onClick = onClick,
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.elevatedCardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.elevatedCardElevation(defaultElevation = 3.dp),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(16.dp)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    Surface(color = dotColor, shape = CircleShape, modifier = Modifier.size(10.dp)) {}
                    Text(
                        (alert.severity ?: "Alert").uppercase(),
                        style = MaterialTheme.typography.labelMedium,
                        color = dotColor,
                        fontWeight = FontWeight.Bold,
                    )
                    if (alert.isAcknowledged) {
                        Text(
                            "• ACK",
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
        }
    }
}
