package cz.cleansia.partner.navigation

import android.content.Intent
import android.net.Uri

/**
 * Result of parsing a deep link
 */
sealed class DeepLinkDestination {
    data object Dashboard : DeepLinkDestination()
    data object Orders : DeepLinkDestination()
    data class OrderDetails(val orderId: String) : DeepLinkDestination()
    data object Invoices : DeepLinkDestination()
    data class InvoiceDetails(val invoiceId: String) : DeepLinkDestination()
    data object Profile : DeepLinkDestination()
    data class ConfirmEmail(val email: String, val token: String) : DeepLinkDestination()
    data object Unknown : DeepLinkDestination()
}

/**
 * Handles deep link parsing for the app.
 *
 * Supported deep links:
 * - cleansia://partner/dashboard
 * - cleansia://partner/orders
 * - cleansia://partner/orders/{orderId}
 * - cleansia://partner/invoices
 * - cleansia://partner/invoices/{invoiceId}
 * - cleansia://partner/profile
 * - https://partner.cleansia.cz/confirm-email?email=xxx&token=xxx
 * - https://partner.cleansia.cz/orders/{orderId}
 * - https://partner.cleansia.cz/invoices/{invoiceId}
 */
object DeepLinkHandler {

    /**
     * Parse an intent and extract the deep link destination
     */
    fun parseIntent(intent: Intent?): DeepLinkDestination? {
        if (intent?.action != Intent.ACTION_VIEW) return null
        return parseUri(intent.data)
    }

    /**
     * Parse a URI and return the appropriate destination
     */
    fun parseUri(uri: Uri?): DeepLinkDestination? {
        uri ?: return null

        val scheme = uri.scheme
        val host = uri.host
        val pathSegments = uri.pathSegments

        return when {
            // Custom scheme: cleansia://partner/...
            scheme == "cleansia" && host == "partner" -> {
                parsePathSegments(pathSegments)
            }

            // App links: https://partner.cleansia.cz/...
            scheme == "https" && host == "partner.cleansia.cz" -> {
                // Check for email confirmation link
                if (uri.path?.startsWith("/confirm-email") == true) {
                    val email = uri.getQueryParameter("email")
                    val token = uri.getQueryParameter("token")
                    if (!email.isNullOrBlank() && !token.isNullOrBlank()) {
                        DeepLinkDestination.ConfirmEmail(email, token)
                    } else {
                        DeepLinkDestination.Unknown
                    }
                } else {
                    parsePathSegments(pathSegments)
                }
            }

            else -> null
        }
    }

    /**
     * Parse path segments to determine the destination
     */
    private fun parsePathSegments(segments: List<String>): DeepLinkDestination {
        return when {
            segments.isEmpty() -> DeepLinkDestination.Dashboard

            segments.size == 1 -> {
                when (segments[0].lowercase()) {
                    "dashboard" -> DeepLinkDestination.Dashboard
                    "orders" -> DeepLinkDestination.Orders
                    "invoices" -> DeepLinkDestination.Invoices
                    "profile" -> DeepLinkDestination.Profile
                    else -> DeepLinkDestination.Unknown
                }
            }

            segments.size == 2 -> {
                val section = segments[0].lowercase()
                val id = segments[1]

                when (section) {
                    "orders" -> DeepLinkDestination.OrderDetails(id)
                    "invoices" -> DeepLinkDestination.InvoiceDetails(id)
                    else -> DeepLinkDestination.Unknown
                }
            }

            else -> DeepLinkDestination.Unknown
        }
    }

    /**
     * Convert a DeepLinkDestination to the appropriate NavRoute
     */
    fun toNavRoute(destination: DeepLinkDestination): NavRoute? {
        return when (destination) {
            is DeepLinkDestination.Dashboard -> NavRoute.Dashboard
            is DeepLinkDestination.Orders -> NavRoute.Orders
            is DeepLinkDestination.OrderDetails -> NavRoute.OrderDetails(destination.orderId)
            is DeepLinkDestination.Invoices -> NavRoute.Invoices
            is DeepLinkDestination.InvoiceDetails -> NavRoute.InvoiceDetails(destination.invoiceId)
            is DeepLinkDestination.Profile -> NavRoute.Profile
            is DeepLinkDestination.ConfirmEmail -> NavRoute.ConfirmEmail(destination.email)
            is DeepLinkDestination.Unknown -> null
        }
    }
}
