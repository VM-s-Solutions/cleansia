package cz.cleansia.customer.core.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * `ApiErrorParser` resolves a backend key through `Resources.getIdentifier` and,
 * on a miss, returns the raw key — so an unmapped refusal reaches the snackbar as
 * "recurring_booking.membership_required". Nothing breaks the build, and the key
 * set staying consistent across the five locales does not help either: a key
 * missing from *all five* is perfectly consistent. Only naming the keys the app
 * can actually receive catches that.
 */
class BackendKeyStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /**
     * `BusinessErrorMessage.RecurringTemplate*` — every refusal
     * `CreateRecurringBooking` can answer the customer app with.
     */
    private val recurringBookingKeys = listOf(
        "recurring_booking.ends_on_before_start",
        "recurring_booking.membership_required",
        "recurring_booking.no_services_or_packages",
        "recurring_booking.not_found",
        "recurring_booking.not_owned_by_user",
        "recurring_booking.saved_address_not_found",
        "recurring_booking.starts_on_in_past",
    )

    /**
     * Every refusal `Auth/GoogleAuth` can answer this app with. `auth.social_account_not_found` is
     * the one a sign-in gets for an identity that matches no account — reachable from the sign-in
     * screen, which carries no terms tick and so may never provision.
     */
    private val googleAuthKeys = listOf(
        "auth.apple_type_error",
        "auth.external_type_error",
        "auth.google_type_error",
        "auth.internal_type_error",
        "auth.invalid_google_token",
        "auth.social_account_not_found",
        "validation.invalid_password",
    )

    /**
     * `CreateOrder` refuses a promo the server will not honour instead of booking at full price, so
     * every `BusinessErrorMessage.Promo*` key is a live 400 on the mobile customer host. Error code
     * `PromoCode`; the preview road (`ValidatePromoCode`) still answers the enum name at 200.
     */
    private val createOrderPromoKeys = listOf(
        "promo.not_found",
        "promo.inactive",
        "promo.expired",
        "promo.not_yet_valid",
        "promo.global_limit_reached",
        "promo.per_user_limit_reached",
        "promo.below_minimum_order_amount",
        "promo.currency_mismatch",
        "promo.requires_account",
    )

    /**
     * `QuoteOrder` judges the address's country and the selection's price rows itself now, so a
     * refusal that used to come only from the address resolver or from Create can land on the
     * live quote.
     */
    private val quoteOrderMarketKeys = listOf(
        "country.not_serviced",
        "currency.invalid",
        "order.selected_services.invalid",
        "order.selected_package.invalid",
    )

    /**
     * `Subscribe` resolves the chosen market's currency and picks the plan's price row in it
     * (ADR-0059); a market with no row, or a Stripe Customer already billed in another currency,
     * refuses with a key the snackbar must be able to say.
     */
    private val subscribeMarketKeys = listOf(
        "membership.plan.not_priced_in_currency",
        "membership.stripe_customer_currency_locked",
        "country.not_serviced",
    )

    /**
     * `OperatorTenantScopeBehavior` runs before validation on every anonymous request that names a
     * market — `Register`, `GoogleAuth`, `Referral/Validate`, `Order/Quote`, `Order/QuotePlusSavings`
     * and both `CreateOrder` routes. A country that is not a market is refused with the existing key;
     * a market nobody operates is refused with the new one (ADR-0061 D3).
     */
    private val operatorScopeKeys = listOf(
        "country.not_serviced",
        "tenant.not_found",
    )

    /**
     * `CreateOrder` refuses an address whose country is operated by another company than the one the
     * caller's account belongs to (ADR-0061 D6); reachable from both the order and the payment route.
     */
    private val createOrderOperatorKeys = listOf(
        "order.country_operator_mismatch",
    )

    /**
     * The terms gate: `Register` and `CreateOrder` refuse a call that asserts no tick — a registration
     * always, a booking unless the signed-in account already holds both legal consents. The social
     * sign-ups keep `auth.social_account_not_found` for the same absence, because there the flag is
     * what tells the sign-in screen from the sign-up screen.
     */
    private val termsTickKeys = listOf(
        "consent.terms_not_accepted",
    )

    /** `CreateOrder` and `CreateRecurringBooking` refuse every booking without the request, a consented account included. */
    private val earlyPerformanceKeys = listOf(
        "consent.early_performance_not_requested",
    )

    /**
     * The archived-company write guard (ADR-0064 D3): a review, a dispute or a cancellation against a
     * company frozen for archive is refused at the commit and answered 409 with this key on every
     * customer route, so the snackbar must be able to say it.
     */
    private val archivedCompanyKeys = listOf(
        "tenant.archived",
    )

    /**
     * Cash only for a signed-in customer whose booking one cleaner does alone (`BookingPolicy.AllowsCash`).
     * Refused on `CreateOrder`, `CreateRecurringBooking`, `UpdateRecurringBooking` and
     * `ConfirmRecurringOrder` — all four reachable from this app.
     */
    private val cashEligibilityKeys = listOf(
        "order.cash_not_available",
    )

    /**
     * A start off the quarter-hour, outside 08:00–19:45 in the market's clock, or more than 60 days
     * ahead: refused by `QuoteOrder`, `CreateOrder`, `CreateRecurringBooking` and `UpdateRecurringBooking`.
     */
    private val bookingWindowKeys = listOf(
        "order.cleaning_date.outside_booking_window",
    )

    /**
     * Past the start with a cleaner assigned, the cancel and both previews refuse; the guest
     * `ReportGuestNoShow` refuses before the start, once the job runs, and on a closed order.
     */
    private val cleanerNoShowKeys = listOf(
        "order.start_passed_cannot_cancel",
        "order.cleaner_already_started",
        "order.start_time_not_reached",
        "order.already_cancelled",
        "order.already_completed",
    )

    /** Every refusal `ConfirmRecurringOrder` can answer. */
    private val confirmRecurringKeys = listOf(
        "common.required",
        "common.invalid_enum_value",
        "order.not_found",
        "order.already_cancelled",
        "order.payment_already_paid",
        "order.recurring_already_confirmed",
        "order.cleaning_date.below_lead_time",
        "order.cash_not_available",
        "order.unpaid_receivable",
        "order.cash_open_bookings_limit_reached",
        "order.payment_gateway_unavailable",
        "order.invalid_status_transition",
        "user.not_found",
        "consent.terms_not_accepted",
    )

    /**
     * An open receivable refuses every booking, cash or card; cash also needs room under the upcoming
     * cash bookings not yet paid. Refused on `CreateOrder`, both recurring writes and `ConfirmRecurringOrder`.
     */
    private val cashStandingKeys = listOf(
        "order.unpaid_receivable",
        "order.cash_open_bookings_limit_reached",
    )

    /** Every refusal `SavedCard/CreateSetupIntent` and `SavedCard/Remove` can answer. */
    private val savedCardKeys = listOf(
        "saved_card.consent_not_accepted",
        "saved_card.not_found",
        "country.not_serviced",
        "order.payment_gateway_unavailable",
        "user.not_found",
        "common.required",
    )

    /** Every refusal `Receivable/CreatePayLink` can answer. */
    private val receivableKeys = listOf(
        "receivable.not_found",
        "receivable.not_open",
        "order.payment_gateway_unavailable",
        "common.required",
    )

    /** Every refusal `GrantConsent` and `WithdrawConsent` can answer beyond the common validators. */
    private val consentKeys = listOf(
        "gdpr.consent_not_editable",
        "gdpr.consent_already_granted",
        "gdpr.consent_not_found",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun declared(locale: String): Set<String> {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"([^\"]+)\"")
            .findAll(file.readText())
            .map { it.groupValues[1] }
            .toSet()
    }

    /** The transform in `ApiErrorParser.resolveStringByErrorKey`, kept in step by its own test. */
    private fun resourceName(key: String) = "error_" + key.replace('.', '_').lowercase()

    @Test
    fun `every recurring-booking refusal resolves to a sentence in all five locales`() {
        assertAllResolve(recurringBookingKeys)
    }

    @Test
    fun `every google-auth refusal resolves to a sentence in all five locales`() {
        assertAllResolve(googleAuthKeys)
    }

    @Test
    fun `every promo refusal CreateOrder can answer resolves to a sentence in all five locales`() {
        assertAllResolve(createOrderPromoKeys)
    }

    @Test
    fun `every market refusal QuoteOrder can answer resolves to a sentence in all five locales`() {
        assertAllResolve(quoteOrderMarketKeys)
    }

    @Test
    fun `every market refusal Subscribe can answer resolves to a sentence in all five locales`() {
        assertAllResolve(subscribeMarketKeys)
    }

    @Test
    fun `every operator-scope refusal an anonymous request can answer resolves to a sentence in all five locales`() {
        assertAllResolve(operatorScopeKeys)
    }

    @Test
    fun `the operator mismatch CreateOrder can answer resolves to a sentence in all five locales`() {
        assertAllResolve(createOrderOperatorKeys)
    }

    @Test
    fun `the terms refusal Register and CreateOrder can answer resolves to a sentence in all five locales`() {
        assertAllResolve(termsTickKeys)
    }

    @Test
    fun `the early-performance refusal a booking or schedule can answer resolves to a sentence in all five locales`() {
        assertAllResolve(earlyPerformanceKeys)
    }

    @Test
    fun `the archived-company refusal every write can answer resolves to a sentence in all five locales`() {
        assertAllResolve(archivedCompanyKeys)
    }

    @Test
    fun `the cash refusal a booking or schedule can answer resolves to a sentence in all five locales`() {
        assertAllResolve(cashEligibilityKeys)
    }

    @Test
    fun `the booking-window refusal a booking or schedule can answer resolves to a sentence in all five locales`() {
        assertAllResolve(bookingWindowKeys)
    }

    @Test
    fun `every post-start cancel and no-show refusal resolves to a sentence in all five locales`() {
        assertAllResolve(cleanerNoShowKeys)
    }

    @Test
    fun `every refusal ConfirmRecurringOrder can answer resolves to a sentence in all five locales`() {
        assertAllResolve(confirmRecurringKeys)
    }

    @Test
    fun `every consent refusal resolves to a sentence in all five locales`() {
        assertAllResolve(consentKeys)
    }

    @Test
    fun `every cash-standing refusal a booking or schedule can answer resolves to a sentence in all five locales`() {
        assertAllResolve(cashStandingKeys)
    }

    /** The server answers an open receivable with `order.unpaid_receivable` alone since the ban covers card too. */
    @Test
    fun `the retired cash-only debt refusal is gone from every locale`() {
        val left = locales.filter { resourceName("order.cash_unpaid_receivable") in declared(it) }
        assertTrue("error_order_cash_unpaid_receivable is still declared in $left", left.isEmpty())
    }

    @Test
    fun `every saved-card refusal resolves to a sentence in all five locales`() {
        assertAllResolve(savedCardKeys)
    }

    @Test
    fun `every pay-link refusal resolves to a sentence in all five locales`() {
        assertAllResolve(receivableKeys)
    }

    private fun assertAllResolve(keys: List<String>) {
        val raw = locales.flatMap { locale ->
            val declared = declared(locale)
            keys
                .filterNot { resourceName(it) in declared }
                .map { "$locale/${resourceName(it)} ($it)" }
        }
        if (raw.isNotEmpty()) {
            fail(
                "these refusals reach the snackbar as their raw backend key: " +
                    raw.joinToString(", "),
            )
        }
    }
}
