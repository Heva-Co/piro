package co.heva.piro.android

import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.systemBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.compose.viewModel
import co.heva.piro.android.alert.AlertDetailScreen
import co.heva.piro.android.alert.AlertDetailViewModel
import co.heva.piro.android.alert.AlertDetailViewModelFactory
import co.heva.piro.android.main.MainScaffold
import co.heva.piro.android.ui.theme.PiroTheme
import co.heva.piro.android.login.LoginScreen
import co.heva.piro.android.login.LoginViewModel
import co.heva.piro.android.login.LoginViewModelFactory
import co.heva.piro.android.push.AlarmPlayer
import co.heva.piro.android.push.DeviceRegistrar

/**
 * Single-activity host. Requests the notification permission, drives the login → home flow, and handles
 * the SSO round-trip: an SSO button opens the provider in a Custom Tab, and the browser redirect back
 * to piro://oauth/callback re-enters this activity (singleTask) where the code+state are exchanged.
 */
class MainActivity : ComponentActivity() {

    private val viewModel: LoginViewModel by viewModels {
        val services = (application as PiroApp).services
        LoginViewModelFactory(services.api, services.tokenStorage, DeviceRegistrar(services.api))
    }

    private val notificationsGranted = mutableStateOf(false)

    /** Set when a page notification is opened (piro://alert/{id}); drives navigation to the detail screen. */
    private val openAlertId = mutableStateOf<Int?>(null)

    private val requestNotifications = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        notificationsGranted.value = granted
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge() // system bar icons adapt to the content theme (light/dark)
        super.onCreate(savedInstanceState)
        maybeRequestNotifications()

        setContent {
            // Follow the OS theme (dark/light), matching Piro web's `system` default.
            PiroTheme {
                Surface(
                    color = MaterialTheme.colorScheme.background,
                    modifier = Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.systemBars),
                ) {
                    val state by viewModel.state.collectAsState()
                    val granted by remember { notificationsGranted }
                    val alertId = openAlertId.value

                    when {
                        !state.signedIn -> LoginScreen(
                            state = state,
                            onEmailChange = viewModel::onEmailChange,
                            onPasswordChange = viewModel::onPasswordChange,
                            onSignIn = viewModel::signIn,
                            onSsoClick = { provider -> openSso(provider.id) },
                        )
                        alertId != null -> AlertDetailRoute(
                            alertId = alertId,
                            onBack = { openAlertId.value = null },
                        )
                        else -> MainScaffold(
                            userName = state.email.ifBlank { "you" },
                            notificationsGranted = granted,
                            onOpenAlert = { id -> openAlertId.value = id },
                            onSignOut = { viewModel.signOut() },
                        )
                    }
                }
            }
        }

        handleIntent(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleIntent(intent)
    }

    private fun handleIntent(intent: Intent?) {
        handleSsoRedirect(intent)
        handleAlertDeepLink(intent)
    }

    /** piro://alert/{id} — opening a page: stop the alarm and route to the alert detail. */
    private fun handleAlertDeepLink(intent: Intent?) {
        val data = intent?.data ?: return
        if (data.scheme != "piro" || data.host != "alert") return
        AlarmPlayer.stop()
        data.lastPathSegment?.toIntOrNull()?.let { openAlertId.value = it }
    }

    @Composable
    private fun AlertDetailRoute(alertId: Int, onBack: () -> Unit) {
        val services = (application as PiroApp).services
        val vm: AlertDetailViewModel = viewModel(
            key = "alert-$alertId",
            factory = AlertDetailViewModelFactory(services.api, alertId),
        )
        val detailState by vm.state.collectAsState()
        AlertDetailScreen(
            state = detailState,
            onAcknowledge = vm::acknowledge,
            onBack = onBack,
        )
    }

    private fun maybeRequestNotifications() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            requestNotifications.launch(android.Manifest.permission.POST_NOTIFICATIONS)
        } else {
            notificationsGranted.value = true
        }
    }

    private fun openSso(providerId: String) {
        val services = (application as PiroApp).services
        val url = services.api.oidcStartUrl(providerId)
        CustomTabsIntent.Builder().build().launchUrl(this, Uri.parse(url))
    }

    /** Handles a piro://oauth/callback?code=…&state=… redirect delivered as a VIEW intent. */
    private fun handleSsoRedirect(intent: Intent?) {
        val data = intent?.data ?: return
        if (data.scheme != "piro" || data.host != "oauth") return
        val code = data.getQueryParameter("code") ?: return
        val state = data.getQueryParameter("state") ?: return
        viewModel.completeSso(code, state)
    }
}
