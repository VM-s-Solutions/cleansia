package cz.cleansia.customer.navigation

import kotlinx.serialization.Serializable

/**
 * Typed Compose-Navigation routes (Navigation 2.8+).
 *
 * Each destination is either a `@Serializable object` (no args) or a
 * `@Serializable data class` (args as constructor params). The Navigation
 * runtime serializes the type to a route string and deserializes the args
 * into a [androidx.lifecycle.SavedStateHandle] keyed by property name —
 * which is why ViewModels keep working with `savedStateHandle.get<String>("orderId")`
 * after this migration. (Equivalent typed access:
 * `savedStateHandle.toRoute<OrderDetailRoute>()`.)
 *
 * Why typed routes:
 *  - Compile-time safety: no more `"orders/{orderId}"` string-template typos.
 *  - Single source of truth: arg name + arg type live next to the route name.
 *  - Cheap to refactor: rename a route, the compiler walks every call site.
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
     * Recurring booking create form. Optional [orderId] pre-fills
     * services/packages/rooms/etc. from a past Completed order (Path B).
     * Without it, the form is a blank slate (Path A).
     */
    @Serializable
    data class CreateRecurringBooking(val orderId: String? = null)

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
