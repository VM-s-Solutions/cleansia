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
