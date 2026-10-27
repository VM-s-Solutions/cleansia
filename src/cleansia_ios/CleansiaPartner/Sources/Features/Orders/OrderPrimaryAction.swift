import CleansiaPartnerApi
import Foundation

/// The one valid primary action for an order, resolved from status × ownership ×
/// after-photos. The single machine shared by the detail footer, the Available
/// Take CTA, and the Active-row swipe — mirrors
/// `OrderPrimaryAction.kt:54-126`. Pure value type, no UI.
enum OrderPrimaryAction: Equatable {
    case take
    case notifyOnTheWay
    case start
    /// InProgress & mine, a CASH order not yet settled — the cash must be
    /// collected and marked before the order can complete (the server's
    /// payment-settled guard, surfaced early).
    case collectCash
    /// InProgress & mine on a crew job whose caller has not marked their part done: "I'm done".
    case markDone
    /// InProgress & mine on a crew job whose caller has marked their part done while someone else on it
    /// has not: nothing to do but wait for them.
    case awaitingCrew
    case complete
    /// InProgress & mine on a crew job everyone on it has marked done: the shared "Complete job", which any
    /// of them sends.
    case completeJob
    /// InProgress & mine but no "after" photo yet — the slide is blocked and a
    /// hint is shown instead (the server's after-photos guard, surfaced early).
    /// `cashPending` carries the cash the photo gate is hiding, so the hint can
    /// state the whole remaining sequence instead of only the photo.
    case completeBlocked(cashPending: Bool)
    case none

    /// The `OrderAction` this maps to for the in-flight discriminator, or nil
    /// when there is nothing to dispatch.
    var orderAction: OrderAction? {
        switch self {
        case .take: .take
        case .notifyOnTheWay: .notifyOnTheWay
        case .start: .start
        case .collectCash: .markCashCollected
        case .markDone: .markDone
        case .complete, .completeJob: .complete
        case .awaitingCrew, .completeBlocked, .none: nil
        }
    }

    // One parameter per fact the action is decided by, each named at the call site.
    // swiftlint:disable:next function_parameter_count
    static func action(
        for status: OrderStatus?,
        isMine: Bool,
        hasAfterPhotos: Bool,
        isCashPayment: Bool,
        isPaymentSettled: Bool,
        crew: CrewFinish
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
            // OnTheWay: the assignee starts. A NON-assignee can still take a seat — the status is on
            // the ORDER, not on a person, so one crew mate setting off used to remove the whole job
            // from every board with its other seats empty (owner ruling 2026-09-06). The server gate
            // is the authority; browse-detail only returns this order while a seat remains.
            return isMine ? .start : .take
        case ._4:
            // InProgress: only the assignee completes, gated first on an
            // after-photo, then on payment — a cash order settles only once the
            // cleaner collects (the `OrderPrimaryAction.kt` canComplete →
            // needsCashCollection ordering).
            // Same as OnTheWay: the work has begun short-crewed and a seat is open. A late joiner
            // is worth more to the customer than an empty seat.
            guard isMine else { return .take }
            return finishing(
                crew: crew,
                hasAfterPhotos: hasAfterPhotos,
                cashPending: isCashPayment && !isPaymentSettled
            )
        case ._1, ._5, ._6, .none:
            // Pending / Completed / Cancelled / unknown — no action.
            return .none
        }
    }

    /// A crew job's mark has no photo or payment rule of its own; those stay the shared Complete's.
    private static func finishing(crew: CrewFinish, hasAfterPhotos: Bool, cashPending: Bool) -> OrderPrimaryAction {
        switch crew {
        case .myPartOpen: return .markDone
        case .waitingForCrew: return .awaitingCrew
        case .solo, .everyoneDone: break
        }
        guard hasAfterPhotos else { return .completeBlocked(cashPending: cashPending) }
        if cashPending { return .collectCash }
        return crew == .everyoneDone ? .completeJob : .complete
    }
}
