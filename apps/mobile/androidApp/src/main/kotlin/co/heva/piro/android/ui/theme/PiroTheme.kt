package co.heva.piro.android.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

/**
 * Piro's brand palette, ported from the web/admin shadcn "neutral" theme (apps/web/src/app/globals.css).
 * The UI is monochrome/grayscale by design, with color reserved for meaning: the brand blue flame
 * ([PiroBlue]) is the one accent, and green/amber/red carry status semantics.
 */
object PiroColors {
    val Blue = Color(0xFF3D96FE) // brand flame accent

    // Neutral surfaces (dark theme — the on-call app defaults to dark, matching a night-shift tool).
    val BackgroundDark = Color(0xFF252525)
    val SurfaceDark = Color(0xFF343434)
    val SurfaceVariantDark = Color(0xFF404040)
    val OnDark = Color(0xFFFBFBFB)
    val MutedDark = Color(0xFFB4B4B4)
    val BorderDark = Color(0x1AFFFFFF)

    // Light theme neutrals.
    val BackgroundLight = Color(0xFFFFFFFF)
    val SurfaceLight = Color(0xFFFFFFFF)
    val SurfaceVariantLight = Color(0xFFF6F6F6)
    val OnLight = Color(0xFF252525)
    val MutedLight = Color(0xFF8D8D8D)
    val BorderLight = Color(0xFFE8E8E8)

    // Status semantics (shared across themes).
    val Up = Color(0xFF22C55E)
    val Degraded = Color(0xFFF59E0B)
    val Down = Color(0xFFEF4444)
    val Identified = Color(0xFFF97316)
    val CriticalRed = Color(0xFFB91C1C)
}

private val DarkColors = darkColorScheme(
    primary = PiroColors.Blue,
    onPrimary = Color.White,
    background = PiroColors.BackgroundDark,
    onBackground = PiroColors.OnDark,
    surface = PiroColors.SurfaceDark,
    onSurface = PiroColors.OnDark,
    surfaceVariant = PiroColors.SurfaceVariantDark,
    onSurfaceVariant = PiroColors.MutedDark,
    outline = PiroColors.BorderDark,
    error = PiroColors.Down,
)

private val LightColors = lightColorScheme(
    primary = PiroColors.Blue,
    onPrimary = Color.White,
    background = PiroColors.BackgroundLight,
    onBackground = PiroColors.OnLight,
    surface = PiroColors.SurfaceLight,
    onSurface = PiroColors.OnLight,
    surfaceVariant = PiroColors.SurfaceVariantLight,
    onSurfaceVariant = PiroColors.MutedLight,
    outline = PiroColors.BorderLight,
    error = PiroColors.Down,
)

@Composable
fun PiroTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        content = content,
    )
}
