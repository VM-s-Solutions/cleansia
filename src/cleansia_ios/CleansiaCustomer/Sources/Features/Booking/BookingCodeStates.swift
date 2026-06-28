import CleansiaCustomerApi
import Foundation

struct BookingQuote: Equatable {
    let totalPrice: Double
    let originalSubtotal: Double
    let currencyId: String
    let currencyCode: String
    let servicesSubtotal: Double
    let packagesSubtotal: Double
    let extrasSubtotal: Double
    let tierDiscountAmount: Double
    let membershipDiscountAmount: Double
    let expressSurchargeApplied: Bool
    let expressSurchargeAmount: Double

    init(
        totalPrice: Double,
        originalSubtotal: Double = 0,
        currencyId: String = "",
        currencyCode: String = "",
        servicesSubtotal: Double = 0,
        packagesSubtotal: Double = 0,
        extrasSubtotal: Double = 0,
        tierDiscountAmount: Double = 0,
        membershipDiscountAmount: Double = 0,
        expressSurchargeApplied: Bool = false,
        expressSurchargeAmount: Double = 0
    ) {
        self.totalPrice = totalPrice
        self.originalSubtotal = originalSubtotal
        self.currencyId = currencyId
        self.currencyCode = currencyCode
        self.servicesSubtotal = servicesSubtotal
        self.packagesSubtotal = packagesSubtotal
        self.extrasSubtotal = extrasSubtotal
        self.tierDiscountAmount = tierDiscountAmount
        self.membershipDiscountAmount = membershipDiscountAmount
        self.expressSurchargeApplied = expressSurchargeApplied
        self.expressSurchargeAmount = expressSurchargeAmount
    }

    init(from response: QuoteOrderResponse) {
        self.init(
            totalPrice: response.totalPrice ?? 0,
            originalSubtotal: response.originalSubtotal ?? 0,
            currencyId: response.currencyId ?? "",
            currencyCode: response.currencyCode ?? "",
            servicesSubtotal: response.servicesSubtotal ?? 0,
            packagesSubtotal: response.packagesSubtotal ?? 0,
            extrasSubtotal: response.extrasSubtotal ?? 0,
            tierDiscountAmount: response.tierDiscountAmount ?? 0,
            membershipDiscountAmount: response.membershipDiscountAmount ?? 0,
            expressSurchargeApplied: response.expressSurchargeApplied ?? false,
            expressSurchargeAmount: response.expressSurchargeAmount ?? 0
        )
    }
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
