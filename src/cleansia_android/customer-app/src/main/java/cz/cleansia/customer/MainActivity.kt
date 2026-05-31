package cz.cleansia.customer
import cz.cleansia.core.snackbar.GlobalSnackbarHost

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import androidx.navigation.compose.rememberNavController
import cz.cleansia.customer.core.notifications.NotificationDeepLink
import cz.cleansia.customer.core.notifications.PushTokenSessionObserver
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.settings.ThemePreference
import cz.cleansia.customer.navigation.CleansiaNavHost
import cz.cleansia.customer.ui.theme.CleansiaTheme
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow

/**
 * App-wide settings exposed via CompositionLocal so any screen can read the
 * current theme/language without threading a ViewModel through every call site.
 */
val LocalAppSettings = staticCompositionLocalOf { AppSettings() }

@AndroidEntryPoint
class MainActivity : androidx.appcompat.app.AppCompatActivity() {
    @Inject lateinit var settingsRepository: AppSettingsRepository

    /**
     * Drives FCM device registration off of (auth-session × FCM-token)
     * state instead of discrete event hooks. Attached on every cold
     * start; see [PushTokenSessionObserver] for full rationale.
     */
    @Inject lateinit var pushTokenSessionObserver: PushTokenSessionObserver

    /**
     * Notification-tap deep link, in typed-route form (e.g.
     * `Routes.OrderDetail(orderId)`). Set from the launching intent in
     * [onCreate] and from subsequent taps in [onNewIntent]; consumed in
     * the composition by a `LaunchedEffect` that navigates and clears.
     *
     * Holding state on the Activity (rather than at the NavHost level)
     * is the only way `onNewIntent` — which fires outside composition —
     * can drive navigation.
     */
    private val pendingDeepLink = MutableStateFlow<Any?>(null)

    /**
     * Android 13+ runtime grant for `POST_NOTIFICATIONS`. Required for any
     * notification (FCM-driven or local) to actually display — without it
     * the system silently drops the call to `NotificationManager.notify`.
     * No-op on older API levels (permission was install-time there).
     *
     * We're not interested in the result here — the OS handles "denied"
     * by hiding the toast, which is the correct user-facing behavior, and
     * the NotificationsScreen lets the user toggle per-category prefs
     * separately. If they re-enable later via system settings, we pick it
     * up automatically without re-prompting.
     */
    private val requestNotificationPermission =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { /* result ignored */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // Cold-start tap on a notification — resolve the deep link before
        // the NavHost composes so the LaunchedEffect picks it up immediately.
        pendingDeepLink.value = NotificationDeepLink.resolve(intent)
        maybeRequestNotificationPermission()
        // Start observing (session × FCM-token) so the device gets
        // registered on every cold start with an existing session, not
        // only on discrete login / rotation events.
        pushTokenSessionObserver.attach(lifecycleScope)
        setContent {
            val settings by settingsRepository.settings
                .collectAsStateWithLifecycle(initialValue = AppSettings())

            val darkTheme = when (settings.theme) {
                ThemePreference.System -> isSystemInDarkTheme()
                ThemePreference.Light -> false
                ThemePreference.Dark -> true
            }

            CompositionLocalProvider(LocalAppSettings provides settings) {
                CleansiaTheme(darkTheme = darkTheme) {
                    // Root surface uses `background` (Slate50) so the floating
                    // island bottom nav blends into the page rather than sitting
                    // on a white band. Default `surface` (#FFFFFF) creates a
                    // visible rectangle around the pill on light mode.
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background,
                    ) {
                        // NOTE: previously this Surface had a root-level
                        // Modifier.clickable that called focusManager.clearFocus()
                        // + keyboardController.hide() to implement "tap outside
                        // to dismiss". That broke ALL text input app-wide:
                        // tapping any OutlinedTextField fired the parent click
                        // handler immediately after focus was granted, hiding
                        // the keyboard before the user could type.
                        //
                        // If we want "tap outside to dismiss" again, attach it
                        // to specific scrollable form Columns (where TextField
                        // children DO consume their own taps), not to a root
                        // Surface that sits behind every screen.
                        val navController = rememberNavController()

                        // Notification deep links — `pendingDeepLink` is set by
                        // onCreate (cold start) + onNewIntent (running app),
                        // emits a typed Routes.X value, gets navigated and
                        // cleared. Latest-wins; multiple taps in flight just
                        // collapse to the last one.
                        val pendingRoute by pendingDeepLink.collectAsStateWithLifecycle()
                        LaunchedEffect(pendingRoute) {
                            val route = pendingRoute ?: return@LaunchedEffect
                            navController.navigate(route)
                            pendingDeepLink.value = null
                        }

                        // Stack the snackbar host on top of the nav host so it
                        // floats over every screen — NavHost below, snackbar above.
                        androidx.compose.foundation.layout.Box(
                            modifier = Modifier.fillMaxSize(),
                        ) {
                            CleansiaNavHost(
                                navController = navController,
                                settingsRepository = settingsRepository,
                            )
                            cz.cleansia.core.snackbar.GlobalSnackbarHost()
                        }
                    }
                }
            }
        }
    }

    /**
     * Notification tapped while the app is already running. launchMode
     * is "singleTop" in the manifest so this fires instead of stacking
     * a fresh activity instance. Resolved deep link flows through
     * [pendingDeepLink] → LaunchedEffect → navController.
     */
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        pendingDeepLink.value = NotificationDeepLink.resolve(intent)
    }

    private fun maybeRequestNotificationPermission() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) return
        val granted = ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.POST_NOTIFICATIONS,
        ) == PackageManager.PERMISSION_GRANTED
        if (!granted) {
            requestNotificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
}
