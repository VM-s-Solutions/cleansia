package cz.cleansia.partner.navigation

import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import cz.cleansia.core.auth.SessionEvent
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaErrorState
import cz.cleansia.core.ui.components.WordmarkSplash
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.features.auth.ConfirmEmailScreen
import cz.cleansia.partner.features.auth.ForgotPasswordScreen
import cz.cleansia.partner.features.auth.LoginScreen
import cz.cleansia.partner.features.auth.RegisterScreen
import cz.cleansia.partner.features.auth.SessionViewModel
import cz.cleansia.partner.features.devices.DevicesScreen
import cz.cleansia.partner.features.earnings.EarningsSummaryScreen
import cz.cleansia.partner.features.invoices.InvoiceDetailScreen
import cz.cleansia.partner.features.invoices.InvoicesListScreen
import cz.cleansia.partner.features.main.MainScaffold
import cz.cleansia.partner.features.notifications.NotificationsScreen
import cz.cleansia.partner.features.onboarding.OnboardingScreen
import cz.cleansia.partner.features.orders.OnboardingChainViewModel
import cz.cleansia.partner.features.orders.OrderDetailScreen
import cz.cleansia.partner.features.orders.PendingOffersScreen
import cz.cleansia.partner.features.orders.RegistrationLockScreen
import cz.cleansia.partner.features.orders.isRegistrationComplete
import cz.cleansia.partner.features.payroll.PeriodPayScreen
import cz.cleansia.partner.features.profile.AddressSectionScreen
import cz.cleansia.partner.features.profile.BankSectionScreen
import cz.cleansia.partner.features.profile.DocumentsSectionScreen
import cz.cleansia.partner.features.profile.EmergencySectionScreen
import cz.cleansia.partner.features.profile.IdentificationSectionScreen
import cz.cleansia.partner.features.profile.JobRadiusScreen
import cz.cleansia.partner.features.profile.PersonalSectionScreen
import cz.cleansia.partner.features.profile.ProfileScreen
import cz.cleansia.partner.features.settings.LanguagePickerScreen
import cz.cleansia.partner.features.settings.ThemePickerScreen
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

@Composable
fun PartnerNavHost(navController: NavHostController) {
    // Root-level session observer — reacts to forced sign-outs (refresh
    // failure, server revoked session) by kicking back to Login and clearing
    // the entire back stack.
    val sessionVm: SessionViewModel = hiltViewModel()
    LaunchedEffect(Unit) {
        sessionVm.events.collect { event ->
            when (event) {
                is SessionEvent.ForcedSignOut -> {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(navController.graph.id) { inclusive = true }
                    }
                }
            }
        }
    }

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
                onSignOut = {
                    navController.navigate(NavRoute.Login) {
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
                onNavigateToConfirmEmail = { email ->
                    navController.navigate(NavRoute.ConfirmEmail(email)) {
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
                // Straight to the code screen carrying the address just
                // registered — `confirmEmail` is an anonymous call that mints
                // its own tokens, so the round-trip through Login was never
                // needed. Login stays on the stack (inclusive = false) so the
                // code screen's own back arrow still has somewhere sensible
                // to land.
                onRegisterSuccess = { email ->
                    navController.navigate(NavRoute.ConfirmEmail(email)) {
                        popUpTo(NavRoute.Login) { inclusive = false }
                    }
                },
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
                // Reified popUpTo<T> because ConfirmEmail is now a data class
                // and the bare name is a type, not an instance. `inclusive`
                // is NOT the default and dropping it would leave the code
                // screen on the back stack for an already-confirmed account.
                onNavigateBack = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo<NavRoute.ConfirmEmail> { inclusive = true }
                    }
                },
                onConfirmationSuccess = {
                    // Newly-confirmed accounts always need onboarding;
                    // bounce through Splash for the status check.
                    navController.navigate(NavRoute.Splash) {
                        popUpTo<NavRoute.ConfirmEmail> { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.Main> { entry ->
            MainScaffold(
                onOpenOrderDetails = { id ->
                    navController.navigate(NavRoute.OrderDetail(orderId = id))
                },
                onOpenInvoiceDetails = { id ->
                    navController.navigate(NavRoute.InvoiceDetail(invoiceId = id))
                },
                onOpenProfileSection = { route -> navController.navigate(route) },
                onOpenEarnings = { navController.navigate(NavRoute.Earnings) },
                onOpenNotifications = { navController.navigate(NavRoute.Notifications) },
                onOpenPendingOffers = { navController.navigate(NavRoute.PendingOffers) },
                onSignedOut = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(navController.graph.id) { inclusive = true }
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

        composable<NavRoute.PendingOffers> {
            PendingOffersScreen(
                onNavigateBack = { navController.popBackStack() },
                // A confirmed offer is an ordinary job from that instant on, so it lands on the
                // detail every other taken job lands on.
                onOpenOrder = { id ->
                    navController.navigate(NavRoute.OrderDetail(orderId = id)) {
                        popUpTo(NavRoute.PendingOffers) { inclusive = true }
                    }
                },
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
                        popUpTo(navController.graph.id) { inclusive = true }
                    }
                },
            )
        }

        composable<NavRoute.OrderDetail>(
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
            OrderDetailScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.InvoiceDetail> {
            InvoiceDetailScreen(
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
                    navController.navigate(NavRoute.InvoiceDetail(invoiceId = id))
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
                onNavigateToJobRadius = { navController.navigate(NavRoute.PreferenceJobRadius) },
                onNavigateToDevices = { navController.navigate(NavRoute.Devices) },
                onNavigateToDeleteAccount = { navController.navigate(NavRoute.DeleteAccount) },
                onSignedOut = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(navController.graph.id) { inclusive = true }
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
                onJumpToSection = { chainVm.jumpTo(it, navController) },
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
                    cz.cleansia.partner.features.profile.ADDRESS_PICKER_RESULT_KEY,
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
                        cz.cleansia.partner.features.profile.ADDRESS_PICKER_RESULT_KEY,
                    ] = null
                },
                onboarding = route.onboarding,
                onJumpToSection = { chainVm.jumpTo(it, navController) },
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
                onJumpToSection = { chainVm.jumpTo(it, navController) },
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
                onJumpToSection = { chainVm.jumpTo(it, navController) },
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

        composable<NavRoute.PreferenceJobRadius> {
            JobRadiusScreen(
                onNavigateBack = { navController.popBackStack() },
                onSaved = { navController.popBackStack() },
            )
        }

        composable<NavRoute.Devices> {
            // Self-revoke sign-out rides the root-level forced-sign-out observer above — no
            // per-screen wiring needed.
            DevicesScreen(onNavigateBack = { navController.popBackStack() })
        }

        composable<NavRoute.DeleteAccount> {
            cz.cleansia.partner.features.profile.DeleteAccountScreen(
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable<NavRoute.AddressPicker> {
            cz.cleansia.partner.features.profile.AddressPickerScreen(
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
                            .set(cz.cleansia.partner.features.profile.ADDRESS_PICKER_RESULT_KEY, json)
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
    onSignOut: () -> Unit,
    viewModel: SplashViewModel = hiltViewModel(),
) {
    val outcome by viewModel.outcome.collectAsState()
    val isSigningOut by viewModel.isSigningOut.collectAsState()
    var confirmingSignOut by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) { viewModel.resolve() }

    LaunchedEffect(outcome) {
        when (outcome) {
            SplashOutcome.Authenticated -> onAuthenticated()
            SplashOutcome.Unauthenticated -> onUnauthenticated()
            SplashOutcome.NeedsOnboarding -> onNeedsOnboarding()
            SplashOutcome.NeedsRegistrationLock -> onNeedsRegistrationLock()
            // Stays on this screen. Navigating anywhere is what the old behaviour did wrong.
            SplashOutcome.Unreachable -> Unit
            null -> { /* still resolving */ }
        }
    }

    if (outcome == SplashOutcome.Unreachable) {
        CleansiaErrorState(
            title = stringResource(R.string.splash_unreachable_title),
            message = stringResource(R.string.splash_unreachable_message),
            retryLabel = stringResource(R.string.retry),
            onRetry = { viewModel.resolve() },
            // CleansiaErrorState requires a back affordance and the splash has no back stack, so
            // it signs out — the one thing a stuck cleaner can always do, and the same escape the
            // registration lock offers. It really does sign out now; it used to only navigate.
            backLabel = stringResource(R.string.logout),
            onBack = { confirmingSignOut = true },
        )
    } else {
        WordmarkSplash(
            tagline = stringResource(R.string.splash_tagline),
            showsPartnerLabel = true,
        )
    }

    if (confirmingSignOut) {
        // Dismiss is blocked while the wipe runs: the dialog is the only thing on screen that
        // knows a sign-out is in flight, and closing it would hide that.
        CleansiaDialog(
            onDismiss = { if (!isSigningOut) confirmingSignOut = false },
            title = stringResource(R.string.profile_logout_dialog_title),
            message = stringResource(R.string.profile_logout_dialog_message),
            icon = Icons.AutoMirrored.Outlined.Logout,
            destructive = true,
            confirmEnabled = !isSigningOut,
            confirmLabel = stringResource(R.string.profile_logout_dialog_confirm),
            onConfirm = { viewModel.signOut(onSignOut) },
            dismissLabel = stringResource(R.string.profile_logout_dialog_cancel),
        )
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun SplashBrandingPreview() {
    CleansiaPartnerTheme {
        WordmarkSplash(tagline = stringResource(R.string.splash_tagline), showsPartnerLabel = true)
    }
}

/**
 * Where the splash sends a cleaner.
 *
 * [Unreachable] is NOT a variant of [NeedsRegistrationLock] and must never collapse back into it.
 * The registration lock says "you are not approved yet"; a backend that did not answer says nothing
 * of the kind, and the two used to be indistinguishable — an unanswered call showed the lock screen,
 * whose only exit is signing out. A cleaner on bad signal, or an app reviewer on hotel wifi, was
 * told to upload documents they had already uploaded, with no retry and no way forward.
 *
 * The fail-closed reading stays: an unanswered call still does NOT admit anyone to Orders. What
 * changes is that it now says so honestly and offers to try again.
 */
enum class SplashOutcome { Authenticated, Unauthenticated, NeedsOnboarding, NeedsRegistrationLock, Unreachable }

@HiltViewModel
class SplashViewModel @Inject constructor(
    private val tokenStore: TokenStore,
    private val appSettingsRepository: cz.cleansia.partner.core.settings.AppSettingsRepository,
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
) : ViewModel() {
    // A StateFlow rather than the cold `flow {}` this used to be: a retry has to re-run the check
    // in place. The old shape could only be re-run by rebuilding the whole NavBackStackEntry, which
    // is fine for the post-login bounce and useless for a button.
    private val _outcome = MutableStateFlow<SplashOutcome?>(null)
    val outcome: StateFlow<SplashOutcome?> = _outcome.asStateFlow()

    private val _isSigningOut = MutableStateFlow(false)
    val isSigningOut: StateFlow<Boolean> = _isSigningOut.asStateFlow()

    /**
     * The navigation callback fires only AFTER the repository has cleared the session.
     *
     * That order is load-bearing: this ViewModel is scoped to the Splash back-stack entry and
     * [onSignedOut] pops that entry inclusive, so navigating first would cancel viewModelScope
     * mid-wipe and leave the tokens on disk — which is the bug being fixed.
     *
     * `logout()` always reaches its local wipe (its two network calls are each wrapped in
     * runCatching), so an unreachable server — the only reason this screen exists — cannot
     * leave the session half-cleared. It is not time-bounded the way the iOS twin is, which is
     * why the dialog's confirm disables while it runs rather than pretending it is instant.
     */
    fun signOut(onSignedOut: () -> Unit) {
        if (_isSigningOut.value) return
        viewModelScope.launch {
            _isSigningOut.value = true
            authRepository.logout()
            onSignedOut()
        }
    }

    fun resolve() {
        viewModelScope.launch {
            _outcome.value = null

            val hasSession = tokenStore.current()?.accessToken?.isNotBlank() == true
            if (!hasSession) {
                _outcome.value = if (!appSettingsRepository.hasSeenOnboarding()) {
                    SplashOutcome.NeedsOnboarding
                } else {
                    SplashOutcome.Unauthenticated
                }
                return@launch
            }

            // Authenticated — ask the backend whether onboarding is finished AND admin has
            // approved. Both must be true to land in Main.
            //
            // A FAILURE still never admits anyone to Orders — that fail-closed reading is
            // deliberate and unchanged. But only a transport failure becomes [Unreachable]: a 4xx
            // or 5xx is the backend answering, and "we asked and were refused" is much closer to
            // "not approved" than to "we could not ask". Sending a 403 to a retry screen would
            // loop a cleaner forever on a state no retry can change.
            _outcome.value = when (val result = profileRepository.getRegistrationStatus()) {
                is ApiResult.Success ->
                    if (result.data.isRegistrationComplete()) {
                        SplashOutcome.Authenticated
                    } else {
                        SplashOutcome.NeedsRegistrationLock
                    }
                is ApiResult.Error ->
                    if (result.error is ApiError.Network) {
                        SplashOutcome.Unreachable
                    } else {
                        SplashOutcome.NeedsRegistrationLock
                    }
            }
        }
    }
}
