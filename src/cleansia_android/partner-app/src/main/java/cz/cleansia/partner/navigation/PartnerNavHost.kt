package cz.cleansia.partner.navigation

import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch
import androidx.compose.runtime.rememberCoroutineScope
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.features.auth.screens.ConfirmEmailScreen
import cz.cleansia.partner.features.auth.screens.ForgotPasswordScreen
import cz.cleansia.partner.features.auth.screens.LoginScreen
import cz.cleansia.partner.features.auth.screens.RegisterScreen
import cz.cleansia.partner.features.devices.DevicesScreen
import cz.cleansia.partner.features.earnings.screens.EarningsSummaryScreen
import cz.cleansia.partner.features.invoices.screens.InvoiceDetailsScreen
import cz.cleansia.partner.features.invoices.screens.InvoicesListScreen
import cz.cleansia.partner.features.payroll.PeriodPayScreen
import cz.cleansia.partner.features.main.MainScaffold
import cz.cleansia.partner.features.notifications.screens.NotificationsScreen
import cz.cleansia.partner.features.orders.screens.OrderDetailsScreen
import cz.cleansia.partner.features.orders.screens.RegistrationLockScreen
import cz.cleansia.partner.features.orders.viewmodels.OnboardingChainViewModel
import cz.cleansia.partner.features.orders.viewmodels.isRegistrationComplete
import cz.cleansia.partner.features.profile.screens.AddressSectionScreen
import cz.cleansia.partner.features.profile.screens.BankSectionScreen
import cz.cleansia.partner.features.profile.screens.DocumentsSectionScreen
import cz.cleansia.partner.features.profile.screens.EmergencySectionScreen
import cz.cleansia.partner.features.profile.screens.IdentificationSectionScreen
import cz.cleansia.partner.features.onboarding.screens.OnboardingScreen
import cz.cleansia.partner.features.profile.screens.PersonalSectionScreen
import cz.cleansia.partner.features.profile.screens.ProfileScreen
import cz.cleansia.partner.features.settings.screens.LanguagePickerScreen
import cz.cleansia.partner.features.settings.screens.ThemePickerScreen
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

@Composable
fun PartnerNavHost(navController: NavHostController) {
    NavHost(
        navController = navController,
        startDestination = NavRoute.Splash,
    ) {
        composable<NavRoute.Splash> {
            SplashGate(
                onAuthenticated = {
                    navController.navigate(NavRoute.Main) {
                        popUpTo(NavRoute.Splash) { inclusive = true }
                    }
                },
                onUnauthenticated = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Splash) { inclusive = true }
                    }
                },
                onNeedsOnboarding = {
                    navController.navigate(NavRoute.Onboarding) {
                        popUpTo(NavRoute.Splash) { inclusive = true }
                    }
                },
                onNeedsRegistrationLock = {
                    navController.navigate(NavRoute.RegistrationLock) {
                        popUpTo(NavRoute.Splash) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.Onboarding> {
            // OnboardingScreen owns the "mark as seen" effect via a tiny VM;
            // we just navigate to Login on Skip/Get-started.
            OnboardingScreen(onFinished = {
                navController.navigate(NavRoute.Login) {
                    popUpTo(NavRoute.Onboarding) { inclusive = true }
                }
            })
        }

        composable<NavRoute.Login> {
            LoginScreen(
                onNavigateToRegister = { navController.navigate(NavRoute.Register) },
                onNavigateToForgotPassword = { navController.navigate(NavRoute.ForgotPassword) },
                onNavigateToConfirmEmail = {
                    navController.navigate(NavRoute.ConfirmEmail) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                },
                onLoginSuccess = {
                    // Bounce through Splash so SplashGate re-checks
                    // registration status and routes to Main vs Lock.
                    navController.navigate(NavRoute.Splash) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.Register> {
            RegisterScreen(
                onNavigateToLogin = { navController.popBackStack() },
                onRegisterSuccess = { navController.popBackStack() },
            )
        }

        composable<NavRoute.ForgotPassword> {
            ForgotPasswordScreen(
                onNavigateBack = { navController.popBackStack() },
                onRequestSuccess = { navController.popBackStack() },
            )
        }

        composable<NavRoute.ConfirmEmail> {
            ConfirmEmailScreen(
                onNavigateBack = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.ConfirmEmail) { inclusive = true }
                    }
                },
                onConfirmationSuccess = {
                    // Newly-confirmed accounts always need onboarding;
                    // bounce through Splash for the status check.
                    navController.navigate(NavRoute.Splash) {
                        popUpTo(NavRoute.ConfirmEmail) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.Main> { entry ->
            MainScaffold(
                onOpenOrderDetails = { id ->
                    navController.navigate(NavRoute.OrderDetails(orderId = id))
                },
                onOpenInvoiceDetails = { id ->
                    navController.navigate(NavRoute.InvoiceDetails(invoiceId = id))
                },
                onOpenProfileSection = { route -> navController.navigate(route) },
                onOpenEarnings = { navController.navigate(NavRoute.Earnings) },
                onOpenNotifications = { navController.navigate(NavRoute.Notifications) },
                onSignedOut = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Main) { inclusive = true }
                    }
                },
                // Pass the Main backstack entry so MainScaffold can observe
                // its SavedStateHandle for cross-route tab-switch requests
                // (e.g. Earnings → Invoices via PENDING_TAB_KEY).
                backStackEntry = entry,
            )
        }

        composable<NavRoute.Notifications> {
            NotificationsScreen(
                onNavigateBack = { navController.popBackStack() },
                onOpenRoute = { route -> navController.navigate(route) },
            )
        }

        composable<NavRoute.RegistrationLock> {
            RegistrationLockScreen(
                onFixStep = { destination ->
                    // Profile sections push on top of the lock; when they
                    // popBackStack we land back here and ON_RESUME re-fetches
                    // status. Once complete the VM's onCompleted fires.
                    navController.navigate(destination)
                },
                onCompleted = {
                    navController.navigate(NavRoute.Main) {
                        popUpTo(NavRoute.RegistrationLock) { inclusive = true }
                    }
                },
                onSignedOut = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.RegistrationLock) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.OrderDetails>(
            // Slide-in/out (not fade) for this route specifically.
            // The map is a SurfaceView/TextureView under the hood and
            // doesn't respect Compose alpha during the default fade
            // exit — the sheet content goes translucent first and
            // reveals the still-fully-opaque map behind it, producing
            // the "ghost panel over full-screen map" flash on back.
            // Sliding moves all pixels together so the map slides
            // off with the rest of the screen, no compositing fight.
            enterTransition = {
                slideInHorizontally(
                    initialOffsetX = { it },
                    animationSpec = tween(durationMillis = 260),
                ) + fadeIn(animationSpec = tween(durationMillis = 260))
            },
            exitTransition = {
                slideOutHorizontally(
                    targetOffsetX = { -it / 4 },
                    animationSpec = tween(durationMillis = 220),
                ) + fadeOut(animationSpec = tween(durationMillis = 220))
            },
            popEnterTransition = {
                slideInHorizontally(
                    initialOffsetX = { -it / 4 },
                    animationSpec = tween(durationMillis = 260),
                ) + fadeIn(animationSpec = tween(durationMillis = 260))
            },
            popExitTransition = {
                slideOutHorizontally(
                    targetOffsetX = { it },
                    animationSpec = tween(durationMillis = 260),
                ) + fadeOut(animationSpec = tween(durationMillis = 260))
            },
        ) {
            OrderDetailsScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.InvoiceDetails> {
            InvoiceDetailsScreen(
                onNavigateBack = { navController.popBackStack() },
                onOpenPeriodPay = { payPeriodId, currencyCode ->
                    navController.navigate(NavRoute.PeriodPay(payPeriodId, currencyCode))
                },
            )
        }

        composable<NavRoute.PeriodPay> {
            PeriodPayScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.Earnings> {
            EarningsSummaryScreen(
                onNavigateBack = { navController.popBackStack() },
                // "View all invoices" — drop the cleaner onto the
                // Invoices bottom-nav tab inside Main, not a standalone
                // full-screen list that would hide the nav. We write the
                // target tab ordinal into Main's SavedStateHandle, then
                // pop Earnings; MainScaffold observes the key on
                // recompose and animates the pager to Invoices.
                onOpenInvoices = {
                    val mainEntry = navController.getBackStackEntry(NavRoute.Main)
                    mainEntry.savedStateHandle[
                        cz.cleansia.partner.features.main.PENDING_TAB_KEY
                    ] = cz.cleansia.partner.features.main.MainTab.Invoices.ordinal
                    navController.popBackStack()
                },
            )
        }

        composable<NavRoute.Invoices> {
            // Standalone invoices destination — kept as a fallback /
            // deep-link target. The Earnings → "View all invoices" flow
            // now routes to the Invoices bottom-nav tab inside Main
            // instead of pushing this destination (so the nav stays
            // visible). The main-tab Invoices entry-point still works
            // via the pager with onNavigateBack = null.
            InvoicesListScreen(
                onInvoiceClick = { id ->
                    navController.navigate(NavRoute.InvoiceDetails(invoiceId = id))
                },
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable<NavRoute.Profile> {
            ProfileScreen(
                onNavigateBack = { navController.popBackStack() },
                onNavigateToPersonal = { navController.navigate(NavRoute.ProfilePersonal()) },
                onNavigateToAddress = { navController.navigate(NavRoute.ProfileAddress()) },
                onNavigateToIdentification = { navController.navigate(NavRoute.ProfileIdentification()) },
                onNavigateToBank = { navController.navigate(NavRoute.ProfileBank()) },
                onNavigateToEmergency = { navController.navigate(NavRoute.ProfileEmergency) },
                onNavigateToDocuments = { navController.navigate(NavRoute.ProfileDocuments) },
                onNavigateToLanguage = { navController.navigate(NavRoute.PreferenceLanguage) },
                onNavigateToTheme = { navController.navigate(NavRoute.PreferenceTheme) },
                onNavigateToDevices = { navController.navigate(NavRoute.Devices) },
                onSignedOut = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Profile) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.ProfilePersonal> { entry ->
            val route = entry.toRoute<NavRoute.ProfilePersonal>()
            val chainVm: OnboardingChainViewModel = hiltViewModel()
            PersonalSectionScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = {
                    if (route.onboarding) chainVm.advanceOrFinish(navController)
                    else navController.popBackStack()
                },
                onboarding = route.onboarding,
                chainViewModel = chainVm,
            )
        }
        composable<NavRoute.ProfileAddress> { entry ->
            val route = entry.toRoute<NavRoute.ProfileAddress>()
            val chainVm: OnboardingChainViewModel = hiltViewModel()

            // Watch SavedStateHandle for a picker result. The
            // AddressPicker composable writes the encoded
            // GeocodedAddress under ADDRESS_PICKER_RESULT_KEY before
            // popping; getStateFlow() surfaces it as a hot Flow so the
            // screen recomposes once when the value arrives, then we
            // clear the slot so re-entering doesn't re-apply the same
            // pick.
            val savedHandle = entry.savedStateHandle
            val encodedResult by savedHandle
                .getStateFlow<String?>(
                    cz.cleansia.partner.features.profile.screens.ADDRESS_PICKER_RESULT_KEY,
                    initialValue = null,
                )
                .collectAsState()
            val pickerResult = remember(encodedResult) {
                encodedResult?.let { encoded ->
                    runCatching {
                        kotlinx.serialization.json.Json.decodeFromString(
                            cz.cleansia.core.location.GeocodedAddress.serializer(),
                            encoded,
                        )
                    }.getOrNull()
                }
            }

            AddressSectionScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = {
                    if (route.onboarding) chainVm.advanceOrFinish(navController)
                    else navController.popBackStack()
                },
                onLaunchPicker = { navController.navigate(NavRoute.AddressPicker) },
                pickerResult = pickerResult,
                onPickerResultConsumed = {
                    savedHandle[
                        cz.cleansia.partner.features.profile.screens.ADDRESS_PICKER_RESULT_KEY,
                    ] = null
                },
                onboarding = route.onboarding,
                chainViewModel = chainVm,
            )
        }
        composable<NavRoute.ProfileIdentification> { entry ->
            val route = entry.toRoute<NavRoute.ProfileIdentification>()
            val chainVm: OnboardingChainViewModel = hiltViewModel()
            IdentificationSectionScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = {
                    if (route.onboarding) chainVm.advanceOrFinish(navController)
                    else navController.popBackStack()
                },
                onboarding = route.onboarding,
                chainViewModel = chainVm,
            )
        }
        composable<NavRoute.ProfileBank> { entry ->
            val route = entry.toRoute<NavRoute.ProfileBank>()
            val chainVm: OnboardingChainViewModel = hiltViewModel()
            BankSectionScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = {
                    if (route.onboarding) chainVm.advanceOrFinish(navController)
                    else navController.popBackStack()
                },
                onboarding = route.onboarding,
                chainViewModel = chainVm,
            )
        }
        composable<NavRoute.ProfileEmergency> {
            EmergencySectionScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = { navController.popBackStack() },
            )
        }
        composable<NavRoute.ProfileDocuments> {
            DocumentsSectionScreen(
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable<NavRoute.PreferenceLanguage> {
            LanguagePickerScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.PreferenceTheme> {
            ThemePickerScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.Devices> {
            DevicesScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.AddressPicker> {
            cz.cleansia.partner.features.profile.screens.AddressPickerScreen(
                onBack = { navController.popBackStack() },
                onConfirmed = { picked ->
                    // Stash the pick on the previous backstack entry so
                    // the Address section composable receives it via
                    // `currentBackStackEntry?.savedStateHandle.get(...)`
                    // when it recomposes after the pop.
                    val previous = navController.previousBackStackEntry
                    if (previous != null) {
                        val json = kotlinx.serialization.json.Json.encodeToString(
                            cz.cleansia.core.location.GeocodedAddress.serializer(),
                            picked,
                        )
                        previous.savedStateHandle
                            .set(cz.cleansia.partner.features.profile.screens.ADDRESS_PICKER_RESULT_KEY, json)
                    }
                    navController.popBackStack()
                },
            )
        }

    }
}

@Composable
private fun SplashGate(
    onAuthenticated: () -> Unit,
    onUnauthenticated: () -> Unit,
    onNeedsOnboarding: () -> Unit,
    onNeedsRegistrationLock: () -> Unit,
    viewModel: SplashViewModel = hiltViewModel(),
) {
    val outcome by viewModel.outcome.collectAsState(initial = null)
    LaunchedEffect(outcome) {
        when (outcome) {
            SplashOutcome.Authenticated -> onAuthenticated()
            SplashOutcome.Unauthenticated -> onUnauthenticated()
            SplashOutcome.NeedsOnboarding -> onNeedsOnboarding()
            SplashOutcome.NeedsRegistrationLock -> onNeedsRegistrationLock()
            null -> { /* still resolving */ }
        }
    }
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator()
    }
}

enum class SplashOutcome { Authenticated, Unauthenticated, NeedsOnboarding, NeedsRegistrationLock }

@HiltViewModel
class SplashViewModel @Inject constructor(
    private val tokenStore: TokenStore,
    private val appSettingsRepository: cz.cleansia.partner.core.settings.AppSettingsRepository,
    private val profileRepository: ProfileRepository,
) : ViewModel() {
    val outcome = kotlinx.coroutines.flow.flow {
        val hasSession = tokenStore.current()?.accessToken?.isNotBlank() == true
        if (!hasSession) {
            if (!appSettingsRepository.hasSeenOnboarding()) {
                emit(SplashOutcome.NeedsOnboarding)
            } else {
                emit(SplashOutcome.Unauthenticated)
            }
            return@flow
        }
        // Authenticated — ask the backend whether onboarding is finished
        // AND admin has approved. Both must be true to land in Main; any
        // missing piece sends the cleaner to the registration lock.
        // On API error we default to "locked" so a transient failure
        // doesn't silently let an unapproved cleaner into Orders.
        when (val result = profileRepository.getRegistrationStatus()) {
            is ApiResult.Success ->
                if (result.data.isRegistrationComplete()) {
                    emit(SplashOutcome.Authenticated)
                } else {
                    emit(SplashOutcome.NeedsRegistrationLock)
                }
            is ApiResult.Error -> emit(SplashOutcome.NeedsRegistrationLock)
        }
    }
}

@Composable
private fun ComingSoonPlaceholder(label: String, onBack: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(80.dp))
        Text(
            text = "$label editor — lands in Phase 5 sub-milestone.",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(24.dp))
        CleansiaPrimaryButton(text = "Back", onClick = onBack)
    }
}
