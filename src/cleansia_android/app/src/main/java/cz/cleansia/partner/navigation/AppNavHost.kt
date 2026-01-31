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
import cz.cleansia.partner.features.dashboard.screens.AnalyticsDetailScreen
import cz.cleansia.partner.features.invoices.screens.InvoiceDetailsScreen
import cz.cleansia.partner.features.orders.screens.OrderDetailsScreen
import cz.cleansia.partner.features.profile.screens.ProfileScreen
import cz.cleansia.partner.features.settings.screens.SettingsScreen

@Composable
fun AppNavHost(
    navController: NavHostController = rememberNavController(),
    startDestination: NavRoute,
    userInitials: String = "?",
    deepLinkRoute: NavRoute? = null,
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
        // Onboarding
        composable<NavRoute.Onboarding> {
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
        composable<NavRoute.Login> {
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
                    navController.navigate(NavRoute.Main) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                }
            )
        }

        composable<NavRoute.Register> {
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

        composable<NavRoute.ConfirmEmail> {
            ConfirmEmailScreen(
                onNavigateBack = { navController.popBackStack() },
                onConfirmationSuccess = {
                    navController.navigate(NavRoute.Main) {
                        popUpTo(NavRoute.Login) { inclusive = true }
                    }
                }
            )
        }

        composable<NavRoute.ForgotPassword> {
            ForgotPasswordScreen(
                onNavigateBack = { navController.popBackStack() },
                onRequestSuccess = { navController.popBackStack() }
            )
        }

        // Main Flow (with Bottom Navigation + Swipe)
        composable<NavRoute.Main> {
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
                }
            )
        }

        // Analytics Detail Screen
        composable<NavRoute.Analytics> {
            AnalyticsDetailScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Notifications Screen
        composable<NavRoute.Notifications> {
            cz.cleansia.partner.features.notifications.NotificationsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Account Hub Screen
        composable<NavRoute.AccountHub> {
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
                onNavigateToOrders = { navController.popBackStack() },
                onLogout = {
                    navController.navigate(NavRoute.Login) {
                        popUpTo(NavRoute.Main) { inclusive = true }
                    }
                }
            )
        }

        // Profile Screen (standalone, accessed from TopAppBar menu)
        composable<NavRoute.Profile> {
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
        composable<NavRoute.Settings> {
            SettingsScreen(
                onNavigateBack = { navController.popBackStack() }
            )
        }

        // Detail Screens
        composable<NavRoute.OrderDetails> { backStackEntry ->
            val route = backStackEntry.toRoute<NavRoute.OrderDetails>()
            OrderDetailsScreen(
                orderId = route.orderId,
                onNavigateBack = { navController.popBackStack() }
            )
        }

        composable<NavRoute.InvoiceDetails> { backStackEntry ->
            val route = backStackEntry.toRoute<NavRoute.InvoiceDetails>()
            InvoiceDetailsScreen(
                invoiceId = route.invoiceId,
                onNavigateBack = { navController.popBackStack() }
            )
        }
    }
}
