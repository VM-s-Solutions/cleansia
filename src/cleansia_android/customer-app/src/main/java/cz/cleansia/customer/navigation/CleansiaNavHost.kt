package cz.cleansia.customer.navigation
import cz.cleansia.customer.core.auth.TokenStoreEntryPoint
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.SessionEvent

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavBackStackEntry
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import cz.cleansia.customer.features.booking.BookingSuccessScreen
import cz.cleansia.customer.features.auth.AuthOutcome
import cz.cleansia.customer.features.auth.AuthViewModel
import cz.cleansia.customer.features.auth.EmailVerifyScreen
import cz.cleansia.customer.features.auth.ForgotPasswordScreen
import cz.cleansia.customer.features.auth.SignInScreen
import cz.cleansia.customer.features.auth.SignUpScreen
import cz.cleansia.customer.features.disputes.CreateDisputeScreen
import cz.cleansia.customer.features.disputes.DisputeDetailScreen
import cz.cleansia.customer.features.disputes.DisputesListScreen
import cz.cleansia.customer.features.main.MainShell
import cz.cleansia.customer.features.orders.OrderDetailScreen
import cz.cleansia.customer.features.orders.photos.OrderPhotosScreen
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.features.addresses.AddressManagerScreen
import cz.cleansia.customer.features.profile.AppearanceScreen
import cz.cleansia.customer.features.profile.DevicesScreen
import cz.cleansia.customer.features.profile.EditProfileScreen
import cz.cleansia.customer.features.profile.HelpSupportScreen
import cz.cleansia.customer.features.profile.LanguageScreen
import cz.cleansia.customer.features.profile.NotificationsScreen
import cz.cleansia.customer.features.profile.SecurityScreen
import cz.cleansia.customer.features.rewards.RewardsActivityScreen
import cz.cleansia.customer.features.splash.SplashScreen

// ── Transition specs ──
// Horizontal push (settings drill-down, auth stack) — 280ms
private const val PUSH_DUR = 280

private val pushEnter: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.EnterTransition = {
    slideIntoContainer(
        AnimatedContentTransitionScope.SlideDirection.Left,
        animationSpec = tween(PUSH_DUR),
    ) + fadeIn(tween(PUSH_DUR))
}
private val pushExit: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.ExitTransition = {
    slideOutOfContainer(
        AnimatedContentTransitionScope.SlideDirection.Left,
        animationSpec = tween(PUSH_DUR),
    ) + fadeOut(tween(PUSH_DUR))
}
private val popEnter: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.EnterTransition = {
    slideIntoContainer(
        AnimatedContentTransitionScope.SlideDirection.Right,
        animationSpec = tween(PUSH_DUR),
    ) + fadeIn(tween(PUSH_DUR))
}
private val popExit: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.ExitTransition = {
    slideOutOfContainer(
        AnimatedContentTransitionScope.SlideDirection.Right,
        animationSpec = tween(PUSH_DUR),
    ) + fadeOut(tween(PUSH_DUR))
}

// Celebratory fade (booking success)
private val fadeEnterLong: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.EnterTransition = {
    fadeIn(tween(400))
}
private val fadeExitLong: AnimatedContentTransitionScope<NavBackStackEntry>.() -> androidx.compose.animation.ExitTransition = {
    fadeOut(tween(400))
}

@Composable
fun CleansiaNavHost(
    navController: NavHostController,
    settingsRepository: AppSettingsRepository,
) {
    // Wave 3 Phase R1 — bridge between OrderDetailScreen's "Book again" button
    // and MainShell's booking sheet. OrderDetail sets this then pops back; the
    // Home composable (MainShell) observes it on next composition, opens the
    // sheet pre-filled, and calls back to clear it. NavHost-scoped state so it
    // survives the popBackStack() across the OrderDetail destination removal
    // but doesn't leak across full app restarts.
    var pendingRebookOrderId by androidx.compose.runtime.remember {
        androidx.compose.runtime.mutableStateOf<String?>(null)
    }

    // Root-level session observer — reacts to forced sign-outs (refresh failure,
    // server revoked session, user-initiated logout) by kicking back to SignIn
    // and clearing the entire back stack.
    val sessionVm: cz.cleansia.customer.features.auth.SessionViewModel = hiltViewModel()
    LaunchedEffect(Unit) {
        sessionVm.events.collect { event ->
            when (event) {
                is cz.cleansia.core.auth.SessionEvent.ForcedSignOut -> {
                    navController.navigate(Routes.SignIn) {
                        popUpTo(navController.graph.id) { inclusive = true }
                    }
                }
            }
        }
    }

    NavHost(
        navController = navController,
        startDestination = Routes.Splash,
        modifier = Modifier.fillMaxSize(),
    ) {
        composable<Routes.Splash>(
            enterTransition = fadeEnterLong,
            exitTransition = fadeExitLong,
        ) {
            val context = androidx.compose.ui.platform.LocalContext.current
            // TODO(W3.3): refactor to VM injection — Splash gates on TokenStore
            // pre-Hilt-VM scope; a holder VM here is feasible but the splash is
            // a one-shot route. Acceptable residue mirroring CleansiaApp.kt.
            val tokenStore = androidx.compose.runtime.remember {
                dagger.hilt.android.EntryPointAccessors.fromApplication(
                    context,
                    cz.cleansia.customer.core.auth.TokenStoreEntryPoint::class.java,
                ).tokenStore()
            }

            SplashScreen(
                onContinue = {
                    // Resume the session if the refresh token is still valid.
                    // The access token may have expired — the 401 Authenticator will refresh it.
                    val hasValidSession = tokenStore.current()?.let { !it.isRefreshExpired() } == true
                    val destination: Any = if (hasValidSession) Routes.Home else Routes.SignIn
                    navController.navigate(destination) {
                        popUpTo(Routes.Splash) { inclusive = true }
                    }
                },
            )
        }
        composable<Routes.SignIn>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: AuthViewModel = hiltViewModel()
            val state by vm.uiState.collectAsState()
            val context = androidx.compose.ui.platform.LocalContext.current

            // React to successful outcomes once per emission; VM clears state itself.
            LaunchedEffect(state.outcome) {
                when (val outcome = state.outcome) {
                    AuthOutcome.SignedIn -> {
                        navController.navigate(Routes.Home) {
                            popUpTo(Routes.SignIn) { inclusive = true }
                        }
                        vm.clearState()
                    }
                    is AuthOutcome.NeedsEmailConfirm -> {
                        navController.navigate(Routes.EmailVerify(outcome.email))
                        vm.clearState()
                    }
                    else -> Unit
                }
            }

            SignInScreen(
                onSignInClick = { email, password, rememberMe ->
                    vm.signIn(email, password, rememberMe)
                },
                onForgotPassword = { navController.navigate(Routes.ForgotPassword) },
                onCreateAccount = { navController.navigate(Routes.SignUp) },
                onGoogleSignIn = { vm.signInWithGoogle(context) },
                loading = state.loading,
            )
        }
        composable<Routes.SignUp>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: AuthViewModel = hiltViewModel()
            val state by vm.uiState.collectAsState()
            val context = androidx.compose.ui.platform.LocalContext.current

            LaunchedEffect(state.outcome) {
                when (val outcome = state.outcome) {
                    is AuthOutcome.NeedsEmailConfirm -> {
                        navController.navigate(Routes.EmailVerify(outcome.email))
                        vm.clearState()
                    }
                    // Google sign-in launched from SignUp returns straight to
                    // Home if the account already exists (email is auto-
                    // confirmed by Google). Treat it the same as a SignIn.
                    AuthOutcome.SignedIn -> {
                        navController.navigate(Routes.Home) {
                            popUpTo(Routes.SignIn) { inclusive = true }
                        }
                        vm.clearState()
                    }
                    else -> Unit
                }
            }

            SignUpScreen(
                onRegisterClick = { firstName, lastName, email, password, referralCode ->
                    vm.register(email, password, firstName, lastName, referralCode)
                },
                onLoginClick = { navController.popBackStack() },
                onGoogleSignIn = { vm.signInWithGoogle(context) },
                loading = state.loading,
            )
        }
        composable<Routes.ForgotPassword>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: AuthViewModel = hiltViewModel()
            val state by vm.uiState.collectAsState()

            LaunchedEffect(state.outcome) {
                if (state.outcome is AuthOutcome.PasswordReset) {
                    navController.navigate(Routes.SignIn) {
                        popUpTo(Routes.SignIn) { inclusive = true }
                    }
                    vm.clearState()
                }
            }

            ForgotPasswordScreen(
                onSendCode = { email -> vm.requestPasswordChange(email) },
                onChangePassword = { email, code, newPassword ->
                    vm.changePassword(email, code, newPassword)
                },
                onRegister = {
                    navController.navigate(Routes.SignUp) {
                        popUpTo(Routes.SignIn)
                    }
                },
                onBackToLogin = { navController.popBackStack() },
            )
        }
        composable<Routes.EmailVerify>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) { backStackEntry ->
            val vm: AuthViewModel = hiltViewModel()
            val state by vm.uiState.collectAsState()
            val args = backStackEntry.toRoute<Routes.EmailVerify>()

            LaunchedEffect(state.outcome) {
                if (state.outcome is AuthOutcome.SignedIn) {
                    navController.navigate(Routes.Home) {
                        popUpTo(Routes.SignIn) { inclusive = true }
                    }
                    vm.clearState()
                }
            }

            EmailVerifyScreen(
                email = args.email,
                onVerify = { code -> vm.confirmEmail(code) },
                onResend = { targetEmail -> vm.resendConfirmationEmail(targetEmail) },
                onBack = { navController.popBackStack() },
                loading = state.loading,
            )
        }
        composable<Routes.BookingSuccess>(
            enterTransition = fadeEnterLong,
            exitTransition = fadeExitLong,
        ) { backStackEntry ->
            val args = backStackEntry.toRoute<Routes.BookingSuccess>()
            BookingSuccessScreen(
                confirmationCode = args.confirmationCode,
                orderId = args.orderId,
                // Primary CTA — deep-link into the newly-created order's detail
                // screen. popUpTo(Home, inclusive = false) first clears the
                // success destination off the back stack so pressing Back from
                // the detail returns the user to the Home tab, not to success.
                onViewOrders = {
                    navController.navigate(Routes.OrderDetail(args.orderId)) {
                        popUpTo(Routes.Home) { inclusive = false }
                    }
                },
                onGoHome = {
                    navController.navigate(Routes.Home) {
                        popUpTo(Routes.Home) { inclusive = true }
                    }
                },
            )
        }
        composable<Routes.Home>(
            enterTransition = { fadeIn(tween(PUSH_DUR)) },
            exitTransition = { fadeOut(tween(PUSH_DUR)) },
            popEnterTransition = { fadeIn(tween(PUSH_DUR)) },
            popExitTransition = { fadeOut(tween(PUSH_DUR)) },
        ) {
            MainShell(
                onOrderClick = { orderId ->
                    navController.navigate(Routes.OrderDetail(orderId))
                },
                onLogout = {
                    // sessionVm.logout() calls AuthRepository.logout() which:
                    //  1. Hits POST /api/Auth/Logout (best-effort; no-op if offline)
                    //  2. Clears encrypted token storage
                    //  3. Emits ForcedSignOut(UserInitiated) — observed at the top of CleansiaNavHost,
                    //     which does the navigation. No second navigate call needed here.
                    sessionVm.logout()
                },
                onProfileRow = { key ->
                    when (key) {
                        "edit" -> navController.navigate(Routes.EditProfile)
                        "addresses" -> navController.navigate(Routes.Addresses)
                        "disputes" -> navController.navigate(Routes.Disputes)
                        "notifications" -> navController.navigate(Routes.Notifications)
                        "security" -> navController.navigate(Routes.Security)
                        "devices" -> navController.navigate(Routes.Devices)
                        "appearance" -> navController.navigate(Routes.Appearance)
                        "language" -> navController.navigate(Routes.Language)
                        "help" -> navController.navigate(Routes.HelpSupport)
                        "delete_account" -> navController.navigate(Routes.DeleteAccount)
                        "subscribe_plus" -> navController.navigate(Routes.SubscribePlus)
                        "recurring_bookings" -> navController.navigate(Routes.RecurringBookings)
                    }
                },
                onBookingComplete = { confirmationCode, orderId ->
                    navController.navigate(Routes.BookingSuccess(confirmationCode, orderId))
                },
                onNavigateToEditProfile = {
                    navController.navigate(Routes.EditProfile)
                },
                onNavigateToOnboarding = {
                    navController.navigate(Routes.ProfileOnboarding)
                },
                onOpenRewardsActivity = {
                    navController.navigate(Routes.RewardsActivity)
                },
                onSubscribePlus = {
                    navController.navigate(Routes.SubscribePlus)
                },
                onSetupRecurring = {
                    navController.navigate(Routes.CreateRecurringBooking())
                },
                onManageRecurring = {
                    navController.navigate(Routes.RecurringBookings)
                },
                rebookOrderId = pendingRebookOrderId,
                onRebookConsumed = { pendingRebookOrderId = null },
            )
        }
        composable<Routes.ProfileOnboarding>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: cz.cleansia.customer.features.profile.ProfileViewModel = hiltViewModel()
            val user by vm.currentUser.collectAsState()
            val saving by vm.savingProfile.collectAsState()

            cz.cleansia.customer.features.profile.ProfileOnboardingScreen(
                user = user,
                saving = saving,
                onSkip = { vm.skipOnboarding { navController.popBackStack() } },
                onSave = { phone, birthDate ->
                    vm.completeOnboarding(
                        phoneNumber = phone,
                        birthDate = birthDate,
                        onCompleted = { navController.popBackStack() },
                    )
                },
            )
        }
        composable<Routes.EditProfile>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: cz.cleansia.customer.features.profile.ProfileViewModel = hiltViewModel()
            val user by vm.currentUser.collectAsState()
            val saving by vm.savingProfile.collectAsState()

            EditProfileScreen(
                user = user,
                saving = saving,
                onBack = { navController.popBackStack() },
                onSave = { firstName, lastName, phone, birthDate ->
                    vm.saveProfile(
                        firstName = firstName,
                        lastName = lastName,
                        phoneNumber = phone,
                        birthDate = birthDate,
                        languageCode = user?.preferredLanguageCode,
                        onSaved = { navController.popBackStack() },
                    )
                },
            )
        }
        composable<Routes.Addresses>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            AddressManagerScreen(
                onBack = { navController.popBackStack() },
            )
        }
        composable<Routes.Security>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            SecurityScreen(onBack = { navController.popBackStack() })
        }
        composable<Routes.Devices>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            DevicesScreen(onBack = { navController.popBackStack() })
        }
        composable<Routes.DeleteAccount>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            val vm: cz.cleansia.customer.features.profile.DeleteAccountViewModel = hiltViewModel()
            val loading by vm.loading.collectAsState()

            // Read the current user's email from TokenStore so we can pre-fill the confirm-match check.
            // TODO(W3.3): refactor to VM injection — DeleteAccountViewModel
            // could expose the decoded email; this in-screen TokenStore reach
            // mirrors the splash pattern.
            val context = androidx.compose.ui.platform.LocalContext.current
            val email = androidx.compose.runtime.remember {
                val tokens = dagger.hilt.android.EntryPointAccessors.fromApplication(
                    context,
                    cz.cleansia.customer.core.auth.TokenStoreEntryPoint::class.java,
                ).tokenStore().current()?.accessToken
                tokens?.let { jwt ->
                    cz.cleansia.core.auth.JwtDecoder.extractEmail(jwt)
                }.orEmpty()
            }

            cz.cleansia.customer.features.profile.DeleteAccountScreen(
                userEmail = email,
                onBack = { navController.popBackStack() },
                onConfirmDelete = { vm.deleteAccount() },
                loading = loading,
            )
        }
        composable<Routes.Notifications>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            NotificationsScreen(onBack = { navController.popBackStack() })
        }
        composable<Routes.HelpSupport>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            HelpSupportScreen(onBack = { navController.popBackStack() })
        }
        composable<Routes.Appearance>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            AppearanceScreen(
                onBack = { navController.popBackStack() },
                settingsRepository = settingsRepository,
            )
        }
        composable<Routes.Language>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            LanguageScreen(
                onBack = { navController.popBackStack() },
                settingsRepository = settingsRepository,
            )
        }
        composable<Routes.SubscribePlus>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            cz.cleansia.customer.features.membership.SubscribePlusScreen(
                onBack = { navController.popBackStack() },
                onSubscribed = {
                    // Replace Subscribe with Success so back-press from Success
                    // doesn't dump the user back onto the (now redundant) paywall.
                    navController.navigate(Routes.MembershipSuccess) {
                        popUpTo(Routes.SubscribePlus) { inclusive = true }
                    }
                },
            )
        }
        composable<Routes.MembershipSuccess>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            cz.cleansia.customer.features.membership.MembershipSuccessScreen(
                onPrimary = {
                    // "Back home" — clear the success screen off the back stack
                    // so back-press from the next screen lands on Home, not here.
                    navController.popBackStack(Routes.Home, inclusive = false)
                },
                onSecondary = {
                    // Set up recurring — replace Success with the create wizard
                    // so back from the wizard goes home, not into the celebration.
                    navController.navigate(Routes.CreateRecurringBooking()) {
                        popUpTo(Routes.MembershipSuccess) { inclusive = true }
                    }
                },
            )
        }
        composable<Routes.RecurringBookings>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            cz.cleansia.customer.features.recurring.RecurringBookingsScreen(
                onBack = { navController.popBackStack() },
                onCreateNew = {
                    navController.navigate(Routes.CreateRecurringBooking())
                },
            )
        }
        composable<Routes.CreateRecurringBooking>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            cz.cleansia.customer.features.recurring.CreateRecurringScreen(
                onBack = { navController.popBackStack() },
                onCreated = {
                    // ALWAYS land on the recurring list after submit — Path B
                    // (entry from order detail / home carousel / post-Plus
                    // success) doesn't have RecurringBookings on the back
                    // stack, so popBackStack(RecurringBookings) was a no-op
                    // and the user got stuck on the create screen. Navigate
                    // forward + pop the create route so back from the list
                    // doesn't loop into the wizard again.
                    navController.navigate(Routes.RecurringBookings) {
                        popUpTo<Routes.CreateRecurringBooking> { inclusive = true }
                    }
                },
            )
        }
        composable<Routes.OrderDetail>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) { backStackEntry ->
            val args = backStackEntry.toRoute<Routes.OrderDetail>()
            OrderDetailScreen(
                onBack = { navController.popBackStack() },
                onRebook = {
                    // Stash the id in NavHost-scoped state, then pop back to
                    // MainShell. The Home composable's LaunchedEffect picks it
                    // up on next composition, opens the sheet pre-filled, and
                    // calls onRebookConsumed to null this back out.
                    pendingRebookOrderId = args.orderId
                    navController.popBackStack()
                },
                // Wave 2 Phase 6 — opens CreateDispute pre-filled with this
                // order's id. The screen + VM handle validation + submission.
                onReportIssue = { navController.navigate(Routes.CreateDispute(args.orderId)) },
                // PA14 Path B — opens the Create Recurring form pre-filled
                // from this order. Plus + Completed gating handled inside
                // the screen so non-eligible users never see the CTA.
                onMakeRecurring = { id ->
                    navController.navigate(Routes.CreateRecurringBooking(id))
                },
                onDownloadReceipt = { /* Phase 4 handles this internally via the VM */ },
                onViewPhotos = { navController.navigate(Routes.OrderPhotos(args.orderId)) },
            )
        }
        composable<Routes.OrderPhotos>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            OrderPhotosScreen(onBack = { navController.popBackStack() })
        }

        // ── Rewards activity (Loyalty Phase A — M2) ──
        composable<Routes.RewardsActivity>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            RewardsActivityScreen(onBack = { navController.popBackStack() })
        }

        // ── Disputes (Wave 2 Phase 6) ──
        composable<Routes.Disputes>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            DisputesListScreen(
                onBack = { navController.popBackStack() },
                onDisputeClick = { id -> navController.navigate(Routes.DisputeDetail(id)) },
                onCreateDispute = { navController.navigate(Routes.CreateDispute()) },
            )
        }
        composable<Routes.DisputeDetail>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            DisputeDetailScreen(onBack = { navController.popBackStack() })
        }
        composable<Routes.CreateDispute>(
            enterTransition = pushEnter,
            exitTransition = pushExit,
            popEnterTransition = popEnter,
            popExitTransition = popExit,
        ) {
            CreateDisputeScreen(
                onBack = { navController.popBackStack() },
                onCreated = { disputeId ->
                    // Replace the create screen with the new dispute's
                    // detail screen so back from detail returns to wherever
                    // the user came from (order detail or disputes list).
                    navController.navigate(Routes.DisputeDetail(disputeId)) {
                        popUpTo<Routes.CreateDispute> { inclusive = true }
                    }
                },
            )
        }
    }
}
