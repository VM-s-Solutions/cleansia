package cz.cleansia.customer.features.booking

import kotlinx.datetime.Clock
import kotlinx.datetime.Instant

/**
 * Booking-time pricing helpers — keep in sync with backend
 * `Cleansia.Core.AppServices.Features.Orders.BookingPolicy`.
 *
 *  - Standard lead time: 4h. Bookings ≥4h ahead pay base price.
 *  - Express lead time:  2h. Bookings 2..4h ahead pay +20%.
 *  - Below 2h: rejected by backend validator.
 *
 * Mobile mirrors these on the client so the displayed total matches what the
 * server will charge. The same surcharge is recomputed authoritatively in
 * `CreateOrder.Handler` so a stale client clock can't underpay.
 */
object BookingPricing {
    private const val EXPRESS_LEAD_HOURS = 2.0
    private const val STANDARD_LEAD_HOURS = 4.0
    const val EXPRESS_SURCHARGE_RATE: Double = 0.20

    /** True if the chosen instant falls in the express band relative to [now]. */
    fun requiresExpressSurcharge(cleaningAt: Instant?, now: Instant = Clock.System.now()): Boolean {
        if (cleaningAt == null) return false
        val leadHours = (cleaningAt - now).inWholeMinutes / 60.0
        return leadHours in EXPRESS_LEAD_HOURS..STANDARD_LEAD_HOURS - 0.0001
    }

    /** Surcharge amount in the order currency — zero outside the express band. */
    fun expressSurchargeAmount(basePrice: Double, cleaningAt: Instant?, now: Instant = Clock.System.now()): Double {
        return if (requiresExpressSurcharge(cleaningAt, now)) basePrice * EXPRESS_SURCHARGE_RATE else 0.0
    }

    /** Final price the user pays — base + surcharge. */
    fun finalTotal(basePrice: Double, cleaningAt: Instant?, now: Instant = Clock.System.now()): Double {
        return basePrice + expressSurchargeAmount(basePrice, cleaningAt, now)
    }

    /**
     * Phase B variant — applies whichever of (tierDiscount, promoDiscount) is
     * larger to the raw subtotal FIRST, then adds the express surcharge to the
     * discounted price. Mirrors backend `CreateOrder.Handler` order so the
     * displayed total matches what the user actually pays.
     *
     * Both discount inputs are absolute amounts in the order currency, NOT
     * percentages — caller has already resolved tier % and the promo's
     * fixed/percent rule into a CZK number.
     */
    fun finalTotal(
        basePrice: Double,
        cleaningAt: Instant?,
        tierDiscount: Double,
        promoDiscount: Double,
        now: Instant = Clock.System.now(),
    ): Double {
        val bestDiscount = maxOf(tierDiscount, promoDiscount)
        val discounted = (basePrice - bestDiscount).coerceAtLeast(0.0)
        return discounted + expressSurchargeAmount(discounted, cleaningAt, now)
    }
}
