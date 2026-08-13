package cz.cleansia.customer.navigation

import kotlinx.serialization.Serializable

/**
 * Typed Compose-Navigation routes.
 *
 * The runtime serializes the type to a route string and deserializes args into the SavedStateHandle
 * **keyed by property name**, which is why ViewModels keep reading them by that name.
 */
object Routes {

    // ── Onboarding / Auth ──
    @Serializable
    data object Splash

    @Serializable
    data object SignIn

    @Serializable
    data object SignUp

    @Serializable
    data object ForgotPassword

    /** Email-verify screen; [email] pre-fills the resend-code field when known. */
    @Serializable
    data class EmailVerify(val email: String? = null)

    // ── Main shell + booking ──
    @Serializable
    data object Home

    /** Post-booking celebration screen; both ids are required. */
    @Serializable
    data class BookingSuccess(
        val confirmationCode: String,
        val orderId: String,
    )

    // ── Profile sub-screens ──
    @Serializable
    data object ProfileOnboarding

    @Serializable
    data object EditProfile

    @Serializable
    data object Addresses

    @Serializable
    data object DeleteAccount

    @Serializable
    data object Security

    /** Device self-service — list registered devices, revoke a lost one. */
    @Serializable
    data object Devices

    @Serializable
    data object Notifications

    @Serializable
    data object HelpSupport

    @Serializable
    data object Appearance

    @Serializable
    data object Language

    // ── Cleansia Plus ──
    /** Single subscribe page, reachable from the inactive membership card. */
    @Serializable
    data object SubscribePlus

    /** Post-purchase celebration after Stripe confirms a Plus subscription. */
    @Serializable
    data object MembershipSuccess

    /** Recurring bookings list (Plus-only). */
    @Serializable
    data object RecurringBookings

    /**
     * Recurring booking form. Optional [orderId] pre-fills
     * services/packages/rooms/etc. from a past Completed order (Path B);
     * optional [templateId] loads an existing schedule and submits an update
     * instead of a create (Path C). Neither → blank slate (Path A).
     */
    @Serializable
    data class CreateRecurringBooking(val orderId: String? = null, val templateId: String? = null)

    // ── Orders ──
    @Serializable
    data class OrderDetail(val orderId: String)

    @Serializable
    data class OrderPhotos(val orderId: String)

    // ── Loyalty ──
    @Serializable
    data object RewardsActivity

    // ── Disputes ──
    @Serializable
    data object Disputes

    @Serializable
    data class DisputeDetail(val disputeId: String)

    /**
     * "Report issue" form. [orderId] is null when entered from the FAB on the
     * disputes list (no order context); the screen renders a graceful error
     * state and bounces the user back.
     */
    @Serializable
    data class CreateDispute(val orderId: String? = null)
}
