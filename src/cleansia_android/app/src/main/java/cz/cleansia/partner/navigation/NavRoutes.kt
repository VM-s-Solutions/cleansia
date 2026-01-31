package cz.cleansia.partner.navigation

import kotlinx.serialization.Serializable

/**
 * Navigation routes for the app using type-safe navigation
 */
sealed interface NavRoute {

    // Onboarding
    @Serializable
    data object Onboarding : NavRoute

    // Auth routes
    @Serializable
    data object Login : NavRoute

    @Serializable
    data object Register : NavRoute

    @Serializable
    data class ConfirmEmail(val email: String) : NavRoute

    @Serializable
    data object ForgotPassword : NavRoute

    // Main routes
    @Serializable
    data object Main : NavRoute

    // Tab destinations
    @Serializable
    data object Dashboard : NavRoute

    @Serializable
    data object Orders : NavRoute

    @Serializable
    data object Invoices : NavRoute

    @Serializable
    data object Profile : NavRoute

    @Serializable
    data object Settings : NavRoute

    @Serializable
    data object AccountHub : NavRoute

    @Serializable
    data object Notifications : NavRoute

    @Serializable
    data object Analytics : NavRoute

    // Detail routes
    @Serializable
    data class OrderDetails(val orderId: String) : NavRoute

    @Serializable
    data class InvoiceDetails(val invoiceId: String) : NavRoute
}

/**
 * Bottom navigation tabs
 */
enum class BottomNavTab(val route: NavRoute) {
    DASHBOARD(NavRoute.Dashboard),
    ORDERS(NavRoute.Orders),
    INVOICES(NavRoute.Invoices)
}
