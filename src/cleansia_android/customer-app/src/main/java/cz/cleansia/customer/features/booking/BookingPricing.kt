package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.booking.QuoteOrderResponse
import kotlinx.datetime.Clock
import kotlinx.datetime.Instant

/**
 * Booking-time lead-time bands — keep in sync with backend
 * `Cleansia.Core.AppServices.Features.Orders.BookingPolicy`.
 *
 *  - Standard lead time: 4h. Bookings ≥4h ahead pay base price.
 *  - Express lead time:  2h. Bookings 2..4h ahead pay the express surcharge.
 *  - Below 2h: rejected by backend validator.
 */
object BookingPricing {
    private const val EXPRESS_LEAD_HOURS = 2.0
    private const val STANDARD_LEAD_HOURS = 4.0

    /**
     * Which slots the grid may tag as express, before any quote for that slot exists. The money is
     * never derived from this — the server owns whether a surcharge is charged and what it costs.
     */
    fun requiresExpressSurcharge(cleaningAt: Instant?, now: Instant = Clock.System.now()): Boolean {
        if (cleaningAt == null) return false
        val leadHours = (cleaningAt - now).inWholeMinutes / 60.0
        return leadHours >= EXPRESS_LEAD_HOURS && leadHours < STANDARD_LEAD_HOURS
    }
}

/**
 * The base the server resolves every discount and every minimum-order floor against —
 * `CreateOrder.Handler`'s own `calc.TotalPrice - calc.ExpressSurchargeAmount`. The surcharge goes on
 * *after* the discount, so this is the only base whose verdict the submit reproduces.
 */
val QuoteOrderResponse.preSurchargeSubtotal: Double
    get() = totalPrice - expressSurchargeAmount

/**
 * A discount resolved on [preSurchargeSubtotal], restated against the price it actually comes off.
 * `OrderFactory.DiscountResolution.AsChargedAgainst` does this before the server reports or persists
 * one, so the quote's own tier and membership amounts already arrive in this form and a promo preview
 * does not. The scale is read out of the quote rather than from a rate — no surcharge, no change.
 */
fun QuoteOrderResponse.discountAsCharged(resolvedDiscount: Double): Double {
    val base = preSurchargeSubtotal
    return if (base > 0.0) resolvedDiscount * totalPrice / base else resolvedDiscount
}

/**
 * Every money row the booking summary and the sticky price bar draw, resolved from the server quote
 * in one place so the two can never disagree with each other or with what gets charged.
 *
 * [QuoteOrderResponse.totalPrice] already folds the express surcharge in
 * (`OrderPricingCalculator`: `totalPrice = chargeSubtotal + expressSurchargeAmount`), so the client
 * **splits** it instead of re-applying a percentage — a rate applied on top inflates the screen
 * against the number the order is actually created with. Every discount reaching [resolve] is stated
 * against the charged price, so subtracting it from the gross total reproduces the server's own
 * composition; there is no client-chosen base.
 */
data class BookingPriceSummary(
    val subtotal: Double,
    val expressSurcharge: Double,
    val expressLine: ExpressLine,
    val total: Double,
) {
    enum class ExpressLine { NotExpress, Charged, Waived }

    companion object {
        fun resolve(quote: QuoteOrderResponse?, discount: Double): BookingPriceSummary {
            if (quote == null) {
                return BookingPriceSummary(0.0, 0.0, ExpressLine.NotExpress, 0.0)
            }
            val expressLine = when {
                quote.expressSurchargeWaivedByMembership -> ExpressLine.Waived
                quote.expressSurchargeApplied -> ExpressLine.Charged
                else -> ExpressLine.NotExpress
            }
            return BookingPriceSummary(
                subtotal = quote.preSurchargeSubtotal,
                expressSurcharge = quote.expressSurchargeAmount,
                expressLine = expressLine,
                total = (quote.totalPrice - discount).coerceAtLeast(0.0),
            )
        }
    }
}
