package cz.cleansia.customer

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.compose.rememberNavController
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.settings.ThemePreference
import cz.cleansia.customer.navigation.CleansiaNavHost
import cz.cleansia.customer.ui.theme.CleansiaTheme
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

/**
 * App-wide settings exposed via CompositionLocal so any screen can read the
 * current theme/language without threading a ViewModel through every call site.
 */
val LocalAppSettings = staticCompositionLocalOf { AppSettings() }

@AndroidEntryPoint
class MainActivity : androidx.appcompat.app.AppCompatActivity() {
    @Inject lateinit var settingsRepository: AppSettingsRepository

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
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
                    Surface(modifier = Modifier.fillMaxSize()) {
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

                        // Stack the snackbar host on top of the nav host so it
                        // floats over every screen — NavHost below, snackbar above.
                        androidx.compose.foundation.layout.Box(
                            modifier = Modifier.fillMaxSize(),
                        ) {
                            CleansiaNavHost(
                                navController = navController,
                                settingsRepository = settingsRepository,
                            )
                            cz.cleansia.customer.ui.snackbar.GlobalSnackbarHost()
                        }
                    }
                }
            }
        }
    }
}
