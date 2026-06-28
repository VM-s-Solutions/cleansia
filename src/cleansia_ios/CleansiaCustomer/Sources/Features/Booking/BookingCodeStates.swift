import Foundation

struct BookingQuote: Equatable {
    let totalPrice: Double
    let currencyId: String
    let currencyCode: String
    let servicesSubtotal: Double
    let packagesSubtotal: Double
}

enum BookingQuoteState: Equatable {
    case idle
    case quoting
    case quoted(BookingQuote)

    var quote: BookingQuote? {
        if case let .quoted(value) = self { return value }
        return nil
    }
}

enum PromoCodeState: Equatable {
    case idle
    case validating
    case valid(discountAmount: Double)
    case invalid(code: String?)
}

enum ReferralCodeState: Equatable {
    case idle
    case validating
    case valid(referrerFirstName: String?)
    case invalid(code: String?)
}
