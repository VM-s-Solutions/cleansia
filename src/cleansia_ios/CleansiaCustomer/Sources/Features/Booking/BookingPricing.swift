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

/// Every money row the booking summary and the sticky price bar draw, resolved from the server quote
/// in one place so the two can never disagree with each other or with what gets charged.
///
/// `QuoteOrderResponse.totalPrice` already folds the express surcharge in, so re-applying a percentage
/// on top of it inflates the screen against the number the order is created with.
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
            subtotal: quote.totalPrice - quote.expressSurchargeAmount,
            expressSurcharge: quote.expressSurchargeAmount,
            expressLine: expressLine,
            total: max(quote.totalPrice - discount, 0)
        )
    }
}
