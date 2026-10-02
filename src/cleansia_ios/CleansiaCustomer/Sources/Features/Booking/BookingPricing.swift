import Foundation

enum BookingPricing {
    static let expressLeadHours = 2.0
    static let standardLeadHours = 4.0

    /// Which slots the grid may tag as express, before any quote for that slot exists. The money is
    /// never derived from this — the server owns whether a surcharge is charged and what it costs.
    static func requiresExpressSurcharge(cleaningAt: Date?, now: Date = Date()) -> Bool {
        guard let cleaningAt else { return false }
        let leadHours = cleaningAt.timeIntervalSince(now) / 3600.0
        return leadHours >= expressLeadHours && leadHours < standardLeadHours
    }

    /// How much of `balance` the server spends on an order charged `charged`, when `share` is the most of
    /// an order credit may settle — the mirror of `BookingPolicy.CapCreditForOrder`, floored to whole
    /// minor units and never the whole order, so the confirm step says what Stripe then asks for. The
    /// share comes from the wire; only the formula is copied, and the Android twin is pinned to the same
    /// vectors. Decimal arithmetic, not Double: 100.10 × 0.7 is 70.07 on the server and 70.069… in
    /// binary floating point, which floors a cent short.
    ///
    /// A preview, not a promise: a concurrent booking can drain the balance first, so the screens after
    /// the booking read the order's own `creditAppliedAmount`.
    static func capCreditForOrder(balance: Double, charged: Double, share: Double) -> Double {
        guard balance > 0, charged > 0, share > 0 else { return 0 }
        var ceiling = exactDecimal(charged) * exactDecimal(share)
        var floored = Decimal()
        NSDecimalRound(&floored, &ceiling, 2, .down)
        return NSDecimalNumber(decimal: min(exactDecimal(balance), floored)).doubleValue
    }

    /// The decimal the wire wrote, not the binary approximation `Decimal(Double)` expands it to:
    /// Swift prints a Double as its shortest round-tripping decimal, which is the JSON literal.
    private static func exactDecimal(_ value: Double) -> Decimal {
        Decimal(string: "\(value)", locale: Locale(identifier: "en_US_POSIX")) ?? Decimal(value)
    }

    /// The order detail's formatter, so the booking flow states every figure as the order then shows
    /// it: grouped, and to the minor unit when not whole. A blank code renders the bare amount.
    static func formatTotal(_ total: Double, currencyCode: String) -> String {
        OrdersFormat.price(total, currencyCode: currencyCode)
    }
}

extension BookingQuote {
    /// The base the server resolves every discount and every minimum-order floor against —
    /// `CreateOrder.Handler`'s own `calc.TotalPrice - calc.ExpressSurchargeAmount`. The surcharge goes
    /// on *after* the discount, so this is the only base whose verdict the submit reproduces.
    var preSurchargeSubtotal: Double {
        totalPrice - expressSurchargeAmount
    }

    /// A discount resolved on ``preSurchargeSubtotal``, restated against the price it actually comes
    /// off. `OrderFactory.DiscountResolution.AsChargedAgainst` does this before the server reports or
    /// persists one, so the quote's own tier and membership amounts already arrive in this form and a
    /// promo preview does not. The scale is read out of the quote rather than from a rate — no
    /// surcharge, no change.
    func discountAsCharged(_ resolvedDiscount: Double) -> Double {
        let base = preSurchargeSubtotal
        guard base > 0 else { return resolvedDiscount }
        return resolvedDiscount * totalPrice / base
    }
}

/// Every money row the booking summary and the sticky price bar draw, resolved from the server quote
/// in one place so the two can never disagree with each other or with what gets charged.
///
/// `QuoteOrderResponse.totalPrice` already folds the express surcharge in, so re-applying a percentage
/// on top of it inflates the screen against the number the order is created with. Every discount
/// reaching ``resolve(quote:discount:)`` is stated against the charged price. The dirtiness surcharge
/// is lifted out of the subtotal onto its own row, so subtotal + dirtiness - discount + express is the
/// total.
struct BookingPriceSummary: Equatable {
    enum ExpressLine: Equatable {
        case notExpress
        case charged
        case waived
    }

    let subtotal: Double
    let dirtiness: Dirtiness
    let dirtinessSurcharge: Double
    let expressSurcharge: Double
    let expressLine: ExpressLine
    let total: Double
    /// The credit this booking would spend. Zero unless it is paid by card: credit is a card-only
    /// tender, and it comes off ``total`` only on the card — the sale keeps its size.
    let creditApplied: Double
    /// The customer's balance in the quote's currency, which the cash hint is read against.
    let creditBalance: Double

    /// What the card is asked for once credit has settled its share — the figure Stripe shows.
    var dueOnCard: Double {
        total - creditApplied
    }

    /// `payByCard` decides whether credit applies at all. The cap is taken on the CHARGED total —
    /// promo included — which is why the server hands over the balance and the share, not an answer.
    static func resolve(quote: BookingQuote?, discount: Double, payByCard: Bool = false) -> BookingPriceSummary {
        guard let quote else {
            return BookingPriceSummary(
                subtotal: 0,
                dirtiness: .normal,
                dirtinessSurcharge: 0,
                expressSurcharge: 0,
                expressLine: .notExpress,
                total: 0,
                creditApplied: 0,
                creditBalance: 0
            )
        }
        let expressLine: ExpressLine = if quote.expressSurchargeWaivedByMembership {
            .waived
        } else if quote.expressSurchargeApplied {
            .charged
        } else {
            .notExpress
        }
        let total = max(quote.totalPrice - discount, 0)
        return BookingPriceSummary(
            subtotal: quote.preSurchargeSubtotal - quote.dirtinessSurchargeAmount,
            dirtiness: quote.dirtiness,
            dirtinessSurcharge: quote.dirtinessSurchargeAmount,
            expressSurcharge: quote.expressSurchargeAmount,
            expressLine: expressLine,
            total: total,
            creditApplied: payByCard
                ? BookingPricing.capCreditForOrder(
                    balance: quote.creditBalance,
                    charged: total,
                    share: quote.creditMaxShareOfOrder
                )
                : 0,
            creditBalance: quote.creditBalance
        )
    }
}
