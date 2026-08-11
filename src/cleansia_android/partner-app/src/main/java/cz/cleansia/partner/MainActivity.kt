package cz.cleansia.partner

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.core.content.ContextCompat
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import androidx.navigation.compose.rememberNavController
import cz.cleansia.core.notifications.PushTokenSessionObserver
import cz.cleansia.core.settings.AppLocale
import cz.cleansia.core.snackbar.GlobalSnackbarHost
import cz.cleansia.partner.core.notifications.NotificationDeepLink
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguageSessionObserver
import cz.cleansia.partner.core.settings.ThemePreference
import cz.cleansia.partner.navigation.NavRoute
import cz.cleansia.partner.navigation.PartnerNavHost
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch

/**
 * App-wide settings exposed via CompositionLocal — any composable can read
 * the current theme/language without threading a VM through every call site.
 */
val LocalAppSettings = staticCompositionLocalOf { AppSettings() }

@AndroidEntryPoint
class MainActivity : AppCompatActivity() {

    @Inject lateinit var settingsRepository: AppSettingsRepository

    /**
     * Drives FCM device registration off of (auth-session × FCM-token)
     * state instead of discrete event hooks. Attached on every cold start;
     * see the class doc for why this replaces the old login/email-confirm/
     * onNewToken triggers.
     */
    @Inject lateinit var pushTokenSessionObserver: PushTokenSessionObserver

    /**
     * Re-states the cleaner's chosen language once a sign-in produces a session, so a push the
     * pickers lost to a dead connection stops waiting for the next visit to a picker.
     */
    @Inject lateinit var languageSessionObserver: LanguageSessionObserver

    /**
     * Notification-tap deep link in typed-route form. Set from the launching
     * intent in [onCreate] (cold start) and from [onNewIntent] (running app);
     * consumed by a LaunchedEffect that navigates and clears. Held on the
     * Activity because onNewIntent fires outside composition.
     */
    private val pendingDeepLink = MutableStateFlow<NavRoute?>(null)

    private val requestNotificationPermission =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { /* ignored */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // Cold-start tap on a notification — resolve before the NavHost composes.
        pendingDeepLink.value = NotificationDeepLink.resolve(intent)
        restorePersistedAppLocale()
        maybeRequestNotificationPermission()
        // Start observing (session × FCM-token) so the device gets
        // registered on every cold start with an existing session, not
        // only on the discrete login/rotation events.
        pushTokenSessionObserver.attach(lifecycleScope)
        languageSessionObserver.attach(lifecycleScope)
        setContent {
            val settings by settingsRepository.settings
                .collectAsStateWithLifecycle(initialValue = AppSettings())

            val darkTheme = when (settings.theme) {
                ThemePreference.System -> isSystemInDarkTheme()
                ThemePreference.Light -> false
                ThemePreference.Dark -> true
            }

            CompositionLocalProvider(LocalAppSettings provides settings) {
                CleansiaPartnerTheme(darkTheme = darkTheme) {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background,
                    ) {
                        val navController = rememberNavController()

                        // Notification deep links — pendingDeepLink is set by
                        // onCreate (cold start) + onNewIntent (running app),
                        // navigated, then cleared. Latest-wins.
                        val pendingRoute by pendingDeepLink.collectAsStateWithLifecycle()
                        LaunchedEffect(pendingRoute) {
                            val route = pendingRoute ?: return@LaunchedEffect
                            navController.navigate(route)
                            pendingDeepLink.value = null
                        }

                        // GlobalSnackbarHost layered on top of the nav host so
                        // it floats over every screen. Any VM/repo can publish
                        // via cz.cleansia.core.snackbar.SnackbarController.
                        Box(modifier = Modifier.fillMaxSize()) {
                            PartnerNavHost(navController = navController)
                            GlobalSnackbarHost()
                        }
                    }
                }
            }
        }
    }

    /**
     * Notification tapped while the app is already running. launchMode is
     * "singleTop" in the manifest so this fires instead of stacking a fresh
     * activity. Resolved route flows through [pendingDeepLink] → LaunchedEffect.
     */
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        pendingDeepLink.value = NotificationDeepLink.resolve(intent)
    }

    /**
     * Re-applies the persisted language to the process on every cold start.
     *
     * minSdk is 26 and neither app registers `AppLocalesMetadataHolderService`
     * with `autoStoreLocales`, so on API 26–32 AppCompat holds the per-app
     * locale in a process-scoped static and loses it when the process dies.
     * DataStore kept the choice (it is what `emailLanguageTag()` reads for the
     * confirmation mail), so the cleaner who picked Czech got a Czech email and
     * then reopened the app in English. This closes that gap from the DataStore
     * side — see [AppLocale] for why not the manifest service.
     *
     * [AppLocale.applyIfChanged] is a no-op when the delegate already matches,
     * which is both the API 33+ path and the recreate that the API 26–32 path
     * triggers, so this cannot loop.
     */
    private fun restorePersistedAppLocale() {
        lifecycleScope.launch {
            AppLocale.applyIfChanged(settingsRepository.settings.first().language.tag)
        }
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
