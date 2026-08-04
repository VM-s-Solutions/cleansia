import CleansiaCustomerApi
import Foundation

/// Why a cancellation costs what it costs. The server decides it and ships it on the preview: the two
/// inputs are the caller's own free-cancellation window (a Plus plan's is seeded at 4 hours where the
/// standard is 24 — a *smaller* window is more generous) and whether a cleaner has actually been pulled
/// onto the job. Neither reaches a customer DTO, so no client can classify this even in principle.
enum CancellationTier: Equatable {
    /// Nobody has taken the job — free whatever the clock says.
    case freeNotAccepted
    /// Just booked — free inside the oops window.
    case freeOopsWindow
    /// Free because the cleaning is still far enough away for this customer.
    case freeOutsideWindow
    case partial
    case lastMinute
}

struct CancellationQuote: Equatable {
    let tier: CancellationTier
    let feeAmount: Double
    let refundAmount: Double
    let currencyCode: String?
    let forfeitsExpressWaiver: Bool
}

extension CancellationTier {
    init?(wire: CleansiaCustomerApi.CancellationFeeTier?) {
        switch wire {
        case ._0: self = .freeNotAccepted
        case ._1: self = .freeOopsWindow
        case ._2: self = .freeOutsideWindow
        case ._3: self = .partial
        case ._4: self = .lastMinute
        case .none: return nil
        }
    }
}

extension CancellationQuote {
    /// nil when the tier did not decode. There is nothing to fall back to — the amounts alone cannot say
    /// which sentence the customer is owed, and a rate can be zero for three different reasons.
    init?(_ response: GetCancellationFeePreviewResponse) {
        guard let tier = CancellationTier(wire: response.tier) else { return nil }
        self.init(
            tier: tier,
            feeAmount: response.feeAmount ?? 0,
            refundAmount: response.refundAmount ?? 0,
            currencyCode: response.currencyCode,
            forfeitsExpressWaiver: response.expressWaiverForfeitedOnCancel ?? false
        )
    }
}
