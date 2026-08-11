import CleansiaCore
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
    /// **Refuse, every field of it.** This is the only screen where the customer reads a number and then
    /// decides, so each coercion here pushes them toward the outcome that costs them: a defaulted fee
    /// says *"free"* over a charge, a defaulted refund says *"nothing comes back"* over money that does,
    /// and a defaulted `forfeitsExpressWaiver` says *"you keep this month's free express booking"* to
    /// someone about to spend it. `GetCancellationFeePreview.Response` is a positional record of
    /// non-nullable members with one success path — there is no state this screen can be in where the
    /// server legitimately omits any of them.
    ///
    /// The tier is refused with them and it always was: the amounts alone cannot say which sentence the
    /// customer is owed, and a rate of zero has three different reasons. It refuses rather than
    /// defaulting because `CancellationFeeTier`'s values are frozen and additive, so an unknown one is a
    /// member added since this build — not a free cancellation.
    ///
    /// `currencyCode` stays optional, and that is the same ruling the order detail's `id` gets: the
    /// sheet already falls back to the order's own currency, so a null here has an equally
    /// authoritative replacement and refusing would strand a customer on a booking they want gone.
    ///
    /// The refusal names the field in `WireContractViolation`, and `apiResult` turns it into an
    /// `ApiError.Server(200, …)` carrying that name. **The name is carried for triage, not delivered** —
    /// iOS has no error-reporting sink — and the customer keeps the screen's own specific copy either
    /// way: `CancellationFeeCardModel` maps every `.error` to `.unavailable`, which is the neutral
    /// "we couldn't check the fee" prompt plus a retry, and reads no code.
    init(_ response: GetCancellationFeePreviewResponse) throws {
        tier = try CancellationTier(wire: response.tier).require("tier")
        feeAmount = try response.feeAmount.require("feeAmount")
        refundAmount = try response.refundAmount.require("refundAmount")
        currencyCode = response.currencyCode
        forfeitsExpressWaiver = try response.expressWaiverForfeitedOnCancel
            .require("expressWaiverForfeitedOnCancel")
    }
}
