package co.heva.piro.android.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.unit.dp

/**
 * A placeholder card matching the shape of the real list rows (a leading dot + a few text bars on an
 * [ElevatedCard]) shown while data loads, so the screen doesn't jump when content arrives. Reused by the
 * Schedule, Alerts and Profile screens instead of a bare spinner. Bars pulse via a shared shimmer alpha.
 */
@Composable
fun SkeletonCard(modifier: Modifier = Modifier, lines: Int = 2) {
    val transition = rememberInfiniteTransition(label = "skeleton")
    val alpha by transition.animateFloat(
        initialValue = 1f,
        targetValue = 0.45f,
        animationSpec = infiniteRepeatable(tween(900), RepeatMode.Reverse),
        label = "shimmer",
    )
    val bar = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.12f)

    ElevatedCard(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.elevatedCardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.elevatedCardElevation(defaultElevation = 3.dp),
    ) {
        Row(
            Modifier.padding(16.dp).alpha(alpha),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Spacer(Modifier.size(10.dp).background(bar, CircleShape))
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Spacer(Modifier.width(160.dp).height(12.dp).background(bar, RoundedCornerShape(6.dp)))
                if (lines > 1) Spacer(Modifier.width(110.dp).height(10.dp).background(bar, RoundedCornerShape(5.dp)))
                if (lines > 2) Spacer(Modifier.width(90.dp).height(10.dp).background(bar, RoundedCornerShape(5.dp)))
            }
        }
    }
}

/** A stack of [SkeletonCard]s — the list-shaped loading state. */
@Composable
fun SkeletonList(modifier: Modifier = Modifier, count: Int = 5, lines: Int = 2) {
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        repeat(count) { SkeletonCard(lines = lines) }
    }
}
