package cz.cleansia.partner.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.toRoute
import cz.cleansia.partner.features.account.screens.AccountHubScreen
import cz.cleansia.partner.features.auth.screens.ConfirmEmailScreen
import cz.cleansia.partner.features.auth.screens.ForgotPasswordScreen
import cz.cleansia.partner.features.auth.screens.LoginScreen
import cz.cleansia.partner.features.auth.screens.RegisterScreen
import cz.cleansia.partner.features.onboarding.screens.OnboardingScreen
import cz.cleansia.partner.features.onboarding.screens.ProfileCompletionScreen
import cz.cleansia.partner.features.dashboard.screens.AnalyticsDetailScreen
import cz.cleansia.partner.features.invoices.screens.InvoiceDetailsScreen
import cz.cleansia.partner.features.orders.screens.OrderDetailsScreen
import cz.cleansia.partner.features.profile.screens.ProfileScreen
import cz.cleansia.partner.features.settings.screens.SettingsScreen
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutHorizontally

// Navigation transition helpers
private fun slideInFromRight() = slideInHorizontally(
    initialOffsetX = { it },
    animationSpec = tween(350)
) + fadeIn(tween(250))

private fun slideOutToRight() = slideOutHorizontally(
    targetOffsetX = { it },
    animationSpec = tween(350)
) + fadeOut(tween(200))

private fun slideInFromBottom() = slideInVertically(
    initialOffsetY = { it / 3 },
    animationSpec = tween(350)
) + fadeIn(tween(250))

@Composable
fun AppNavHost(
    navController: NavHostController = rememberNavController(),
    startDestination: NavRoute,
    userInitials: String = "?",
    deepLinkRoute: NavRoute? = null,
    profileCompleted: Boolean = true,
    modifier: Modifier = Modifier
) {
    // Handle deep link navigation
    LaunchedEffect(deepLinkRoute) {
        deepLinkRoute?.let { route ->
            // Navigate to the deep link destination
            when (route) {
                is NavRoute.OrderDetails -> {
                    // First ensure we're on Main, then navigate to details
                    navController.navigate(NavRoute.Main) {
                        popUpTo(0) { inclusive = true }
                    }
                    navController.navigate(route)
                }
                is NavRoute.InvoiceDetails -> {
                    navController.navigate(NavRoute.Main) {
                        popUpTo(0) { inclusive = true }
                    }
                    navController.navigate(route)
                }
                is NavRoute.Dashboard, is NavRoute.Orders, is NavRoute.Invoices, is NavRoute.Profile -> {
                    // These are handled by MainScreen's bottom navigation
                    navController.navigate(NavRoute.Main) {
                        popUpTo(0) { inclusive = true }
                    }
                }
                else -> {
                    navController.navigate(route) {
                        popUpTo(0) { inclusive = true }
                    }
                }
            }
        }
    }

    NavHost(
        navController = navController,
        startDestination = startDestination,
        modifier = modifier
    ) {
        // Onboarding (shown on first launch before auth)
        composable<NavRoute.Onboarding>(
            enterTransition = { fadeIn(tween(400)) },
            exitTransition = { fadeOut(tween(400)) },
            popEnterTransition = { fadeIn(tween(300)) },
            popExitTransition = { fadeOut(tween(300)) }
        ) {
            OnboardingScreen(
                onComplete = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Onboarding) { inclusive = true }
                    }
                },
                onSkip = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Onboarding) { inclusive = true }
                    }
                }
            )
        }

        // Auth Flow
        composable<NavRoute.Login>(
            enterTransition = { fadeIn(tween(300)) },
            exitTransition = { fadeOut(tween(300)) },
            popEnterTransition = { fadeIn(tween(300)) },
            popExitTransition = { fadeOut(tween(300)) }
        ) {
            LoginScreen(
                onNavigateToRegister = {
                    navController.navigate(NavRoute.Register)
                },
                onNavigateToForgotPassword = {
                    navController.navigate(NavRoute.ForgotPassword)
                },
                onNavigateToConfirmEmail = { email ->
                    navController.navigate(NavRoute.ConfirmEmail(email))
                },
                onLoginSuccess = {
                    val destination = if (!profileCompleted) NavRoute.ProfileCompletion else NavRoute.Main
                    navController.navigate(destination) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                }
            )
        }

        composable<NavRoute.Register>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            RegisterScreen(
                onNavigateBack = { navController.popBackStack() },
                onNavigateToLogin = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Register) { inclusive = true }
                    }
                },
                onRegistrationSuccess = { email ->
                    navController.navigate(NavRoute.ConfirmEmail(email)) {
                        popUpTo(NavRoute.Register) { inclusive = true }
                    }
                }
            )
        }

        composable<NavRoute.ConfirmEmail>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            ConfirmEmailScreen(
                onNavigateBack = { navController.popBackStack() },
                onConfirmationSuccess = {
                    val destination = if (!profileCompleted) NavRoute.ProfileCompletion else NavRoute.Main
                    navController.navigate(destination) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                }
            )
        }

        composable<NavRoute.ForgotPassword>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(300)) },
            popExitTransition = { fadeOut(tween(300)) }
        ) {
            ForgotPasswordScreen(
                onNavigateBack = { navController.popBackStack() },
                onRequestSuccess = { navController.popBackStack() }
            )
        }

        // Profile Completion (post-registration)
        composable<NavRoute.ProfileCompletion>(
            enterTransition = { fadeIn(tween(400)) },
            exitTransition = { fadeOut(tween(400)) },
            popEnterTransition = { fadeIn(tween(300)) },
            popExitTransition = { fadeOut(tween(300)) }
        ) {
            ProfileCompletionScreen(
                onComplete = {
                    navController.navigate(NavRoute.Main) {
                        popUpTo(NavRoute.ProfileCompletion) { inclusive = true }
                    }
                }
            )
        }

        // Main Flow (with Bottom Navigation + Swipe)
        composable<NavRoute.Main>(
            enterTransition = { fadeIn(tween(400)) },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { fadeOut(tween(200)) }
        ) {
            MainScreen(
                userInitials = userInitials,
                onNavigateToOrderDetails = { orderId ->
                    navController.navigate(NavRoute.OrderDetails(orderId))
                },
                onNavigateToInvoiceDetails = { invoiceId ->
                    navController.navigate(NavRoute.InvoiceDetails(invoiceId))
                },
                onNavigateToAnalytics = {
                    navController.navigate(NavRoute.Analytics) {
                        launchSingleTop = true
                    }
                },
                onNavigateToAccountHub = {
                    navController.navigate(NavRoute.AccountHub) {
                        launchSingleTop = true
                    }
                },
                onNavigateToProfile = {
                    navController.navigate(NavRoute.Profile) {
                        launchSingleTop = true
                    }
                }
            )
        }

        // Analytics Detail Screen
        composable<NavRoute.Analytics>(
            enterTransition = { slideInFromBottom() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            AnalyticsDetailScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Notifications Screen
        composable<NavRoute.Notifications>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            cz.cleansia.partner.features.notifications.NotificationsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Account Hub Screen
        composable<NavRoute.AccountHub>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            AccountHubScreen(
                onNavigateBack = { navController.navigateUp() },
                onNavigateToProfile = {
                    navController.navigate(NavRoute.Profile) {
                        launchSingleTop = true
                    }
                },
                onNavigateToSettings = {
                    navController.navigate(NavRoute.Settings) {
                        launchSingleTop = true
                    }
                },
                onNavigateToOrderDetails = { orderId ->
                    navController.navigate(NavRoute.OrderDetails(orderId))
                },
                onLogout = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Main) { inclusive = true }
                    }
                }
            )
        }

        // Profile Screen (standalone, accessed from TopAppBar menu)
        composable<NavRoute.Profile>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            ProfileScreen(
                onNavigateBack = { navController.popBackStack() },
                onLogout = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Main) { inclusive = true }
                    }
                }
            )
        }

        // Settings Screen (standalone, accessed from TopAppBar menu)
        composable<NavRoute.Settings>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) {
            SettingsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Detail Screens
        composable<NavRoute.OrderDetails>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) { backStackEntry ->
            val route = backStackEntry.toRoute<NavRoute.OrderDetails>()
            OrderDetailsScreen(
                orderId = route.orderId,
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable<NavRoute.InvoiceDetails>(
            enterTransition = { slideInFromRight() },
            exitTransition = { fadeOut(tween(200)) },
            popEnterTransition = { fadeIn(tween(200)) },
            popExitTransition = { slideOutToRight() }
        ) { backStackEntry ->
            val route = backStackEntry.toRoute<NavRoute.InvoiceDetails>()
            InvoiceDetailsScreen(
                invoiceId = route.invoiceId,
                onNavigateBack = { navController.popBackStack() }
            )
        }
    }
}
