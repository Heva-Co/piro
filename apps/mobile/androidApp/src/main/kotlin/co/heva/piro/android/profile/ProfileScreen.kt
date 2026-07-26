package co.heva.piro.android.profile

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CardDefaults
import co.heva.piro.android.ui.SkeletonCard
import co.heva.piro.android.ui.SkeletonList
import androidx.compose.material3.ElevatedCard
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
import co.heva.piro.android.ui.theme.PiroColors
import co.heva.piro.shared.model.UserProfile
import co.heva.piro.shared.model.generated.UserNotificationPreferenceDto

/**
 * The Profile tab: the signed-in user's identity (avatar, name, email, time zone, roles), their
 * notification-delivery preferences, and a sign-out action. Replaces the earlier "Settings" placeholder.
 */
@Composable
fun ProfileScreen(onSignOut: () -> Unit, modifier: Modifier = Modifier) {
    val app = androidx.compose.ui.platform.LocalContext.current.applicationContext as PiroApp
    val vm: ProfileViewModel = viewModel(factory = ProfileViewModelFactory(app.services.api))
    val state by vm.state.collectAsStateWithLifecycle()

    Column(
        modifier = modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(20.dp),
    ) {
        Text(
            "Profile",
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(bottom = 16.dp),
        )

        when {
            state.loading -> Column(
                Modifier.fillMaxWidth().padding(top = 8.dp),
                verticalArrangement = Arrangement.spacedBy(20.dp),
            ) {
                SkeletonCard(lines = 2)   // identity header
                SkeletonList(count = 4)   // profile fields
            }
            state.profile != null -> ProfileContent(state.profile!!, state.preferences, onSignOut)
            else -> Text(state.error ?: "Profile unavailable.", color = MaterialTheme.colorScheme.error)
        }
    }
}

@Composable
private fun ProfileContent(profile: UserProfile, prefs: List<UserNotificationPreferenceDto>, onSignOut: () -> Unit) {
    // Identity header: avatar + name + email.
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            Modifier.size(56.dp).let { it },
            Alignment.Center,
        ) {
            Surface(color = parseColor(profile.color), shape = CircleShape, modifier = Modifier.size(56.dp)) {
                Box(Modifier.fillMaxSize(), Alignment.Center) {
                    Text(
                        profile.name.take(2).uppercase(),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        color = Color.White,
                    )
                }
            }
        }
        Column(Modifier.padding(start = 16.dp)) {
            Text(profile.name, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onBackground)
            Text(profile.email, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    Field("Time zone", profile.timeZone)
    Field("Roles", profile.roles.joinToString(", ").ifBlank { "—" })
    Field("Sign-in", if (profile.isOidc) "Single sign-on (SSO)" else "Email & password")

    if (prefs.isNotEmpty()) {
        Text(
            "Notification preferences",
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(top = 20.dp, bottom = 8.dp),
        )
        prefs.sortedBy { it.priority }.forEach { PreferenceRow(it) }
    }

    Button(
        onClick = onSignOut,
        colors = ButtonDefaults.buttonColors(containerColor = PiroColors.Down.copy(alpha = 0.15f), contentColor = PiroColors.Down),
        modifier = Modifier.fillMaxWidth().padding(top = 28.dp),
    ) {
        Text("Sign out", fontWeight = FontWeight.Medium)
    }
}

@Composable
private fun Field(label: String, value: String) {
    ElevatedCard(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.elevatedCardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.elevatedCardElevation(defaultElevation = 2.dp),
        modifier = Modifier.fillMaxWidth().padding(top = 12.dp),
    ) {
        Column(Modifier.padding(14.dp)) {
            Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(value, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurface)
        }
    }
}

@Composable
private fun PreferenceRow(pref: UserNotificationPreferenceDto) {
    ElevatedCard(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.elevatedCardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.elevatedCardElevation(defaultElevation = 2.dp),
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
    ) {
        Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(
                    pref.integrationName?.ifBlank { pref.integrationId } ?: pref.integrationId,
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(pref.handle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(
                if (pref.isVerified) "Verified" else "Unverified",
                style = MaterialTheme.typography.labelSmall,
                color = if (pref.isVerified) PiroColors.Up else PiroColors.Degraded,
            )
        }
    }
}

private fun parseColor(hex: String): Color = runCatching {
    Color(("ff" + hex.removePrefix("#")).toLong(16))
}.getOrDefault(PiroColors.Blue)
