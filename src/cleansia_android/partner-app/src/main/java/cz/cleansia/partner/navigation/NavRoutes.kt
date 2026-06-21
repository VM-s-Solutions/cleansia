package cz.cleansia.partner.navigation

import kotlinx.serialization.Serializable

/**
 * Typed routes for Compose Navigation 2.8. Each phase of the rebuild adds
 * its routes here so [PartnerNavHost] can wire them with `composable<>`.
 */
sealed interface NavRoute {
    @Serializable data object Splash : NavRoute
    @Serializable data object Onboarding : NavRoute

    @Serializable data object Login : NavRoute
    @Serializable data object Register : NavRoute
    @Serializable data object ForgotPassword : NavRoute
    @Serializable data object ConfirmEmail : NavRoute

    /**
     * Pre-Main gate for cleaners whose profile / availability / documents
     * aren't complete or whose contract hasn't been Approved yet. Mirrors
     * the partner-web modal overlay that locks every non-profile route.
     */
    @Serializable data object RegistrationLock : NavRoute

    @Serializable data object Main : NavRoute

    @Serializable data object Dashboard : NavRoute

    @Serializable data object Orders : NavRoute
    @Serializable data class OrderDetail(val orderId: String) : NavRoute

    @Serializable data object Invoices : NavRoute
    @Serializable data class InvoiceDetail(val invoiceId: String) : NavRoute

    /**
     * Read-only "my period pay" breakdown for one pay period — reached from
     * an invoice's period card. [currencyCode] rides along because the
     * period-pay DTO carries no currency of its own.
     */
    @Serializable data class PeriodPay(val payPeriodId: String, val currencyCode: String? = null) : NavRoute

    /**
     * Pay & Earnings summary — destination from the dashboard earnings
     * card and the Quick Action "Pay history" tile. Always shows
     * meaningful content (today/week/month earnings, pay-period
     * progress, next payout) even for cleaners with no invoices yet;
     * drills into [Invoices] for full history.
     */
    @Serializable data object Earnings : NavRoute

    /** In-app push-notifications feed — reached from the dashboard bell. */
    @Serializable data object Notifications : NavRoute

    @Serializable data object Profile : NavRoute

    /**
     * The four sections that gate `IsProfileComplete` carry an
     * `onboarding` flag. When true (entered from the registration-lock
     * "Complete profile" CTA), saving jumps to the next missing section
     * instead of popping back — turning four round-trips through the
     * lock into one linear flow. When false (entered from the Profile
     * menu for maintenance edits), saving pops back as before.
     */
    @Serializable data class ProfilePersonal(val onboarding: Boolean = false) : NavRoute
    @Serializable data class ProfileAddress(val onboarding: Boolean = false) : NavRoute
    @Serializable data class ProfileIdentification(val onboarding: Boolean = false) : NavRoute
    @Serializable data class ProfileBank(val onboarding: Boolean = false) : NavRoute

    @Serializable data object ProfileEmergency : NavRoute
    @Serializable data object ProfileDocuments : NavRoute

    /**
     * Full-screen Mapbox picker launched from the Address section. On
     * confirm it writes a serialized `GeocodedAddress` into the
     * previous backstack entry's `SavedStateHandle` under
     * [ADDRESS_PICKER_RESULT_KEY] and pops itself — Address section
     * observes that key on its own `SavedStateHandle` to receive the
     * pick. No route args needed.
     */
    @Serializable data object AddressPicker : NavRoute

    /**
     * Per-preference picker screens — opened from the Preferences
     * rows on the profile landing. Match the customer profile's
     * pattern of one screen per preference instead of inline pickers.
     */
    @Serializable data object PreferenceLanguage : NavRoute
    @Serializable data object PreferenceTheme : NavRoute

    /** Device self-service — list registered devices, revoke a lost one. */
    @Serializable data object Devices : NavRoute
}
