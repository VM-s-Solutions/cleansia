import CleansiaPartnerApi
import Foundation

/// The one valid primary action for an order, resolved from status × ownership ×
/// after-photos. The single machine shared by the detail footer, the Available
/// Take CTA, and the Active-row swipe (architect (c)) — mirrors
/// `OrderPrimaryAction.kt:54-126`. Pure value type, no UI.
enum OrderPrimaryAction: Equatable {
    case take
    case notifyOnTheWay
    case start
    /// InProgress & mine, a CASH order not yet settled — the cash must be
    /// collected and marked before the order can complete (the server's
    /// payment-settled guard, surfaced early).
    case collectCash
    case complete
    /// InProgress & mine but no "after" photo yet — the slide is blocked and a
    /// hint is shown instead (the server's after-photos guard, surfaced early).
    case completeBlocked
    case none

    /// The `OrderAction` this maps to for the in-flight discriminator, or nil
    /// when there is nothing to dispatch.
    var orderAction: OrderAction? {
        switch self {
        case .take: .take
        case .notifyOnTheWay: .notifyOnTheWay
        case .start: .start
        case .collectCash: .markCashCollected
        case .complete: .complete
        case .completeBlocked, .none: nil
        }
    }

    static func action(
        for status: OrderStatus?,
        isMine: Bool,
        hasAfterPhotos: Bool,
        isCashPayment: Bool,
        isPaymentSettled: Bool
    ) -> OrderPrimaryAction {
        switch status {
        case ._0:
            // New: takeable by a non-assignee; a (rare) assigned New has no
            // action — the server lifecycle should have moved it to Confirmed.
            return isMine ? .none : .take
        case ._2:
            // Confirmed: takeable by a non-assignee; the assignee notifies.
            return isMine ? .notifyOnTheWay : .take
        case ._3:
            // OnTheWay: only the assignee starts.
            return isMine ? .start : .none
        case ._4:
            // InProgress: only the assignee completes, gated first on an
            // after-photo, then on payment — a cash order settles only once the
            // cleaner collects (the `OrderPrimaryAction.kt` canComplete →
            // needsCashCollection ordering).
            guard isMine else { return .none }
            guard hasAfterPhotos else { return .completeBlocked }
            if isCashPayment, !isPaymentSettled { return .collectCash }
            return .complete
        case ._1, ._5, ._6, .none:
            // Pending / Completed / Cancelled / unknown — no action.
            return .none
        }
    }
}
