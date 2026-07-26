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
import co.heva.piro.android.push.PushReadinessState

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

    /** Set when a page notification is opened (piro://alert/{id}); drives navigation to the detail screen. */
    private val openAlertId = mutableStateOf<Int?>(null)

    private val requestNotifications = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        // Feed the real readiness pipeline: granting moves us to Registering (the DeviceRegistrar then
        // resolves it to Registered/Failed); denying stays NeedsPermission.
        PushReadinessState.onPermissionResult(granted)
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
                    val readiness by PushReadinessState.state.collectAsState()

                    if (!state.signedIn) {
                        LoginScreen(
                            state = state,
                            onEmailChange = viewModel::onEmailChange,
                            onPasswordChange = viewModel::onPasswordChange,
                            onSignIn = viewModel::signIn,
                            onSsoClick = { provider -> openSso(provider.id) },
                        )
                    } else {
                        // The scaffold owns navigation (including alert detail pushed over the Alerts tab),
                        // so pressing Back from a detail returns to the list, not to a reset tab.
                        MainScaffold(
                            userName = state.email.ifBlank { "you" },
                            readiness = readiness,
                            deepLinkAlertId = openAlertId.value,
                            onDeepLinkConsumed = { openAlertId.value = null },
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

    private fun maybeRequestNotifications() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            requestNotifications.launch(android.Manifest.permission.POST_NOTIFICATIONS)
        } else {
            // Pre-13 has no runtime prompt — permission is implicit; move straight to registering.
            PushReadinessState.onPermissionResult(true)
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
