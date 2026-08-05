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
    let expressSurchargeWaivedByMembership: Bool

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
        expressSurchargeAmount: Double = 0,
        expressSurchargeWaivedByMembership: Bool = false
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
        self.expressSurchargeWaivedByMembership = expressSurchargeWaivedByMembership
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
            expressSurchargeAmount: response.expressSurchargeAmount ?? 0,
            expressSurchargeWaivedByMembership: response.expressSurchargeWaivedByMembership ?? false
        )
    }
}

enum BookingQuoteState: Equatable {
    case idle
    /// Carries the quote that was on screen when the re-quote started, so the
    /// price summary keeps showing the last known total instead of flashing to
    /// 0 for the 400 ms debounce plus the round trip. `nil` only for the very
    /// first quote, where there is genuinely nothing to show yet.
    case quoting(previous: BookingQuote?)
    case quoted(BookingQuote)

    /// Display-only: during `.quoting` this is deliberately stale. Submission
    /// is gated on `lastQuoteRequest` matching the current request, which is
    /// written only on a successful quote, so a stale quote is never priced in.
    var quote: BookingQuote? {
        switch self {
        case .idle: nil
        case let .quoting(previous): previous
        case let .quoted(value): value
        }
    }
}

enum PromoCodeState: Equatable {
    case idle
    case validating
    /// `discountAmount` is stated against the charged price, like every other discount on this screen.
    case valid(discountAmount: Double)
    case invalid(PromoCodeError?)

    var discount: Double {
        if case let .valid(amount) = self { return amount }
        return 0
    }
}

enum ReferralCodeState: Equatable {
    case idle
    case validating
    case valid(referrerFirstName: String?)
    case invalid(ReferralValidationError?)
}
