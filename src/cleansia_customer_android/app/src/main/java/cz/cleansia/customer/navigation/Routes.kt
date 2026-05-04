package cz.cleansia.customer.navigation

object Routes {
    const val Splash = "splash"
    const val SignIn = "auth/signin"
    const val SignUp = "auth/signup"
    const val ForgotPassword = "auth/forgot"
    const val EmailVerify = "auth/verify?email={email}"

    fun emailVerify(email: String?) = if (email.isNullOrBlank()) "auth/verify" else "auth/verify?email=$email"
    const val Home = "home"
    const val BookingSuccess = "booking/success/{confirmationCode}/{orderId}"
    const val OrderDetail = "orders/{orderId}"
    const val OrderPhotos = "orders/{orderId}/photos"

    fun bookingSuccess(confirmationCode: String, orderId: String): String =
        "booking/success/${android.net.Uri.encode(confirmationCode)}/${android.net.Uri.encode(orderId)}"

    const val ProfileOnboarding = "profile/onboarding"
    const val EditProfile = "profile/edit"
    const val Addresses = "profile/addresses"
    const val DeleteAccount = "profile/delete-account"
    const val Security = "profile/security"
    const val Notifications = "profile/notifications"
    const val HelpSupport = "profile/help"
    const val Appearance = "profile/appearance"
    const val Language = "profile/language"

    // Cleansia Plus — single subscribe page, reachable from the inactive
    // membership card on the Profile tab. The management card itself is
    // inline on Profile, no dedicated route.
    const val SubscribePlus = "membership/subscribe"

    // Post-purchase celebration — shown once after Stripe confirms a Plus
    // subscription. Replaces the silent snackbar+pop flow that left users
    // staring at a blank Subscribe screen for a beat.
    const val MembershipSuccess = "membership/success"

    // Recurring bookings — list view with toggle/delete actions. Plus-only
    // perk; the Profile entry point hides for non-Plus users.
    const val RecurringBookings = "membership/recurring"

    // Recurring booking create form. Optional `orderId` query arg pre-fills
    // services/packages/rooms/bathrooms/paymentType/timeOfDay from a past
    // Completed order (Path B). Without the arg, the form is a blank slate
    // (Path A — entry from the empty-state of the recurring list).
    const val CreateRecurringBooking = "membership/recurring/create?orderId={orderId}"
    fun createRecurringBooking(orderId: String? = null): String =
        if (orderId.isNullOrBlank()) "membership/recurring/create"
        else "membership/recurring/create?orderId=${android.net.Uri.encode(orderId)}"

    // ── Rewards (Loyalty Phase A — M2) ──
    // Activity history sub-screen — paged list of all loyalty transactions.
    // Tab itself lives in MainShell as one of the bottom-nav slots.
    const val RewardsActivity = "rewards/activity"

    // ── Disputes (Wave 2 Phase 6) ──
    // Three screens:
    //   - Disputes         → list of the user's disputes (profile entry point)
    //   - DisputeDetail    → a single dispute + messaging thread
    //   - CreateDispute    → "Report issue" form. `orderId` is an optional
    //                        query arg; screen errors out gracefully when
    //                        opened from the FAB (no order context).
    const val Disputes = "disputes"
    const val DisputeDetail = "disputes/{disputeId}"
    const val CreateDispute = "disputes/new?orderId={orderId}"

    fun orderDetail(orderId: String) = "orders/$orderId"
    fun orderPhotos(orderId: String) = "orders/$orderId/photos"

    fun disputeDetail(disputeId: String) = "disputes/${android.net.Uri.encode(disputeId)}"

    /**
     * Build the create-dispute URI. When [orderId] is null/blank, the screen
     * renders a "missing order" error state — the FAB on the list uses this
     * path because there's no single order to attach the dispute to.
     */
    fun createDispute(orderId: String? = null): String =
        if (orderId.isNullOrBlank()) "disputes/new" else "disputes/new?orderId=${android.net.Uri.encode(orderId)}"
}
