package cz.cleansia.partner

import android.content.Intent
import android.os.Bundle
import android.view.ViewTreeObserver
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.stringResource
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.navigation.AppNavHost
import cz.cleansia.partner.navigation.DeepLinkDestination
import cz.cleansia.partner.navigation.DeepLinkHandler
import cz.cleansia.partner.navigation.NavRoute
import cz.cleansia.partner.ui.theme.CleansiaTheme
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : AppCompatActivity() {

    @Inject
    lateinit var tokenManager: TokenManager

    @Inject
    lateinit var preferencesManager: PreferencesManager

    // Store the deep link destination to pass to the composable
    private var pendingDeepLink: DeepLinkDestination? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        val splashScreen = installSplashScreen()

        super.onCreate(savedInstanceState)

        // Keep the native splash (gradient background only) visible until Compose content is drawn.
        // This prevents the brief flash of the app icon before the animated splash starts.
        var isReady = false
        splashScreen.setKeepOnScreenCondition { !isReady }

        enableEdgeToEdge()

        // Restore saved language on startup
        restoreSavedLocale()

        // Handle deep link from launch intent
        if (savedInstanceState == null) {
            pendingDeepLink = DeepLinkHandler.parseIntent(intent)
        }

        setContent {
            val themeMode by preferencesManager.theme.collectAsState(initial = "system")
            val darkTheme = when (themeMode) {
                "dark" -> true
                "light" -> false
                else -> isSystemInDarkTheme()
            }

            CleansiaTheme(darkTheme = darkTheme) {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    CleansiaApp(
                        tokenManager = tokenManager,
                        preferencesManager = preferencesManager,
                        initialDeepLink = pendingDeepLink
                    )
                }
            }
        }

        // Dismiss the native splash as soon as the first frame of Compose content is drawn
        val content = findViewById<android.view.View>(android.R.id.content)
        content.viewTreeObserver.addOnPreDrawListener(
            object : ViewTreeObserver.OnPreDrawListener {
                override fun onPreDraw(): Boolean {
                    isReady = true
                    content.viewTreeObserver.removeOnPreDrawListener(this)
                    return true
                }
            }
        )
    }

    private fun restoreSavedLocale() {
        val currentLocale = AppCompatDelegate.getApplicationLocales()
        val currentTag = if (currentLocale.isEmpty) "" else currentLocale.toLanguageTags()

        runBlocking {
            val savedLanguage = preferencesManager.language.first()
            if (currentTag != savedLanguage) {
                val localeList = LocaleListCompat.forLanguageTags(savedLanguage)
                AppCompatDelegate.setApplicationLocales(localeList)
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        // Handle deep links when app is already running
        val deepLink = DeepLinkHandler.parseIntent(intent)
        if (deepLink != null) {
            pendingDeepLink = deepLink
            // Recreate the content to handle the new deep link
            setContent {
                val themeMode by preferencesManager.theme.collectAsState(initial = "system")
                val darkTheme = when (themeMode) {
                    "dark" -> true
                    "light" -> false
                    else -> isSystemInDarkTheme()
                }

                CleansiaTheme(darkTheme = darkTheme) {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background
                    ) {
                        CleansiaApp(
                            tokenManager = tokenManager,
                            preferencesManager = preferencesManager,
                            initialDeepLink = pendingDeepLink
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun CleansiaApp(
    tokenManager: TokenManager,
    preferencesManager: PreferencesManager,
    initialDeepLink: DeepLinkDestination? = null
) {
    var showSplash by remember { mutableStateOf(true) }

    if (showSplash) {
        cz.cleansia.partner.ui.components.AnimatedSplashScreen(
            onFinished = { showSplash = false }
        )
        return
    }

    val isLoggedIn by tokenManager.isLoggedIn.collectAsState(initial = tokenManager.hasToken())
    val onboardingCompleted by preferencesManager.onboardingCompleted.collectAsState(initial = true)
    val profileCompleted by preferencesManager.profileCompleted.collectAsState(initial = true)

    // Track if we've already consumed the deep link
    var consumedDeepLink by remember { mutableStateOf(false) }

    val startDestination: NavRoute = when {
        // First launch — show onboarding before auth
        !onboardingCompleted -> NavRoute.Onboarding
        // User is logged in
        isLoggedIn -> {
            if (tokenManager.isEmailConfirmed()) {
                if (!profileCompleted) {
                    NavRoute.ProfileCompletion
                } else {
                    NavRoute.Main
                }
            } else {
                tokenManager.getUserEmail()?.let { NavRoute.ConfirmEmail(it) } ?: NavRoute.Login
            }
        }
        // User is not logged in — show login
        else -> NavRoute.Login
    }

    // Determine the deep link route if user is logged in and we have a pending deep link
    val deepLinkRoute: NavRoute? = if (isLoggedIn && tokenManager.isEmailConfirmed() && !consumedDeepLink && initialDeepLink != null) {
        consumedDeepLink = true
        DeepLinkHandler.toNavRoute(initialDeepLink)
    } else {
        null
    }

    val userInitials by tokenManager.userInitials.collectAsState(initial = tokenManager.getUserInitials())
    val focusManager = LocalFocusManager.current

    // Session expired dialog
    var showSessionExpiredDialog by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        tokenManager.sessionExpiredEvent.collect {
            showSessionExpiredDialog = true
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .pointerInput(Unit) {
                detectTapGestures(onTap = {
                    focusManager.clearFocus()
                })
            }
    ) {
        AppNavHost(
            startDestination = startDestination,
            userInitials = userInitials,
            deepLinkRoute = deepLinkRoute,
            profileCompleted = profileCompleted
        )
    }

    if (showSessionExpiredDialog) {
        AlertDialog(
            onDismissRequest = { showSessionExpiredDialog = false },
            title = { Text(stringResource(R.string.session_expired_title)) },
            text = { Text(stringResource(R.string.session_expired_message)) },
            confirmButton = {
                TextButton(onClick = { showSessionExpiredDialog = false }) {
                    Text(stringResource(R.string.ok))
                }
            }
        )
    }
}
