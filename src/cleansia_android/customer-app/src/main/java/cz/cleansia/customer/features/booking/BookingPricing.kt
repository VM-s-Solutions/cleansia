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
    internal const val EXPRESS_LEAD_HOURS = 2.0
    internal const val STANDARD_LEAD_HOURS = 4.0

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
 * How much of [balance] the server will spend on an order charged [charged], when [share] is the most
 * of an order credit may settle. A mirror of backend `BookingPolicy.CapCreditForOrder` — floored to
 * whole minor units, never the whole order — so the confirm step says what Stripe will then ask for.
 * The share comes from the wire; only the formula is copied, and the iOS twin is pinned to the same
 * vectors. Decimal arithmetic, not Double: 100.10 × 0.7 is 70.07 on the server and 70.069… in binary
 * floating point, which floors a cent short.
 *
 * A preview, not a promise: a concurrent booking can drain the balance first, so the screens after the
 * booking read the order's own `creditAppliedAmount`.
 */
internal fun capCreditForOrder(balance: Double, charged: Double, share: Double): Double {
    if (balance <= 0.0 || charged <= 0.0 || share <= 0.0) return 0.0
    val ceiling = charged.toBigDecimal()
        .multiply(share.toBigDecimal())
        .setScale(2, java.math.RoundingMode.FLOOR)
    return minOf(balance.toBigDecimal(), ceiling).toDouble()
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
 *
 * The dirtiness surcharge sits inside the pre-surcharge subtotal, so [subtotal] is stated without it and
 * [dirtinessSurcharge] is its own row: subtotal + dirtiness + express − discount = total.
 */
data class BookingPriceSummary(
    val subtotal: Double,
    val expressSurcharge: Double,
    val expressLine: ExpressLine,
    val total: Double,
    val dirtinessSurcharge: Double = 0.0,
    /**
     * The credit this booking would spend. Zero unless it is paid by card: credit is a card-only
     * tender. Comes off [total] only on the card — the sale keeps its size.
     */
    val creditApplied: Double = 0.0,
) {
    enum class ExpressLine { NotExpress, Charged, Waived }

    /** What the card is asked for once credit has settled its share — the figure Stripe shows. */
    val dueOnCard: Double get() = total - creditApplied

    companion object {
        /**
         * [payByCard] decides whether credit applies at all. The cap is taken on the CHARGED total —
         * promo included — which is why the server hands over the balance and the share rather than
         * an answer.
         */
        fun resolve(quote: QuoteOrderResponse?, discount: Double, payByCard: Boolean = false): BookingPriceSummary {
            if (quote == null) {
                return BookingPriceSummary(0.0, 0.0, ExpressLine.NotExpress, 0.0)
            }
            val expressLine = when {
                quote.expressSurchargeWaivedByMembership -> ExpressLine.Waived
                quote.expressSurchargeApplied -> ExpressLine.Charged
                else -> ExpressLine.NotExpress
            }
            val total = (quote.totalPrice - discount).coerceAtLeast(0.0)
            return BookingPriceSummary(
                subtotal = quote.preSurchargeSubtotal - quote.dirtinessSurchargeAmount,
                expressSurcharge = quote.expressSurchargeAmount,
                expressLine = expressLine,
                total = total,
                dirtinessSurcharge = quote.dirtinessSurchargeAmount,
                creditApplied = if (payByCard) {
                    capCreditForOrder(quote.creditBalance, total, quote.creditMaxShareOfOrder)
                } else {
                    0.0
                },
            )
        }
    }
}
