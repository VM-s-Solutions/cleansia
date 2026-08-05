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

    static func currencySymbol(for code: String) -> String {
        switch code.uppercased() {
        case "CZK": "Kč"
        case "EUR": "€"
        case "USD": "$"
        default: code
        }
    }

    static func formatTotal(_ total: Double, currencyCode: String) -> String {
        String(format: "%.0f %@", total, currencySymbol(for: currencyCode))
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
/// reaching ``resolve(quote:discount:)`` is stated against the charged price.
struct BookingPriceSummary: Equatable {
    enum ExpressLine: Equatable {
        case notExpress
        case charged
        case waived
    }

    let subtotal: Double
    let expressSurcharge: Double
    let expressLine: ExpressLine
    let total: Double

    static func resolve(quote: BookingQuote?, discount: Double) -> BookingPriceSummary {
        guard let quote else {
            return BookingPriceSummary(subtotal: 0, expressSurcharge: 0, expressLine: .notExpress, total: 0)
        }
        let expressLine: ExpressLine = if quote.expressSurchargeWaivedByMembership {
            .waived
        } else if quote.expressSurchargeApplied {
            .charged
        } else {
            .notExpress
        }
        return BookingPriceSummary(
            subtotal: quote.preSurchargeSubtotal,
            expressSurcharge: quote.expressSurchargeAmount,
            expressLine: expressLine,
            total: max(quote.totalPrice - discount, 0)
        )
    }
}
