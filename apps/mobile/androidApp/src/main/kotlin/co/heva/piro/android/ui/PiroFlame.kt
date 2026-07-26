package co.heva.piro.android.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import co.heva.piro.android.ui.theme.PiroColors

/**
 * Piro's brand mark: the blue flame ("Piro" = pyro/fire), ported from the web/admin piro.svg path on a
 * 24×24 viewBox. Drawn with Compose so it scales crisply at any size.
 */
private const val FLAME_PATH =
    "M12.832 21.801c3.126-.626 7.168-2.875 7.168-8.69c0-5.291-3.873-8.815-6.658-10.434" +
        "c-.619-.36-1.342.113-1.342.828v1.828c0 1.442-.606 4.074-2.29 5.169c-.86.559-1.79-.278-1.894-1.298" +
        "l-.086-.838c-.1-.974-1.092-1.565-1.87-.971C4.461 8.46 3 10.33 3 13.11C3 20.221 8.289 22 10.933 22" +
        "q.232 0 .484-.015C10.111 21.874 8 21.064 8 18.444c0-2.05 1.495-3.435 2.631-4.11c.306-.18.663.055.663.41" +
        "v.59c0 .45.175 1.155.59 1.637c.47.546 1.159-.026 1.214-.744c.018-.226.246-.37.442-.256" +
        "c.641.375 1.46 1.175 1.46 2.473c0 2.048-1.129 2.99-2.168 3.357"

@Composable
fun PiroFlame(
    modifier: Modifier = Modifier,
    size: Dp = 48.dp,
    color: Color = PiroColors.Blue,
) {
    val path = rememberFlamePath()
    Canvas(modifier = modifier.size(size)) {
        val scale = this.size.minDimension / 24f
        translate(
            left = (this.size.width - 24f * scale) / 2f,
            top = (this.size.height - 24f * scale) / 2f,
        ) {
            scale(scale, scale, pivot = androidx.compose.ui.geometry.Offset.Zero) {
                drawPath(path, color)
                // Subtle outline keeps the mark legible on any surface.
                drawPath(path, color, style = Stroke(width = 0.5f))
            }
        }
    }
}

@Composable
private fun rememberFlamePath() = androidx.compose.runtime.remember {
    PathParser().parsePathString(FLAME_PATH).toPath()
}
