import CleansiaPartnerApi
import Foundation

/// The one valid primary action for an order, resolved from status × ownership × the crew's
/// standing × after-photos × payment × the clock. The single machine shared by the detail's pinned move, the
/// Available Take CTA, and the Active hero — mirrors
/// `OrderPrimaryAction.kt:54-126`. Pure value type, no UI.
enum OrderPrimaryAction: Equatable {
    case take
    /// Not taken, and it overlaps one of her own jobs: Take is drawn disabled, the reason beside it.
    case takeOverlaps(start: Date, end: Date)
    /// Not taken, and her own jobs are still being read: Take waits, so a tap cannot reach the server first.
    case takeWaiting
    /// Confirmed & mine before the start minus the grace window: the server refuses "on the way" until then.
    case setOffLocked(opensAt: Date)
    case notifyOnTheWay
    case start
    /// InProgress & mine, a CASH order not yet settled — the cash must be
    /// collected and marked before the order can complete (the server's
    /// payment-settled guard, surfaced early).
    case collectCash
    /// InProgress & mine on a crew job whose caller has not marked their part done, once a crew-mate has or the after
    /// photo is in: "I'm done".
    case markDone
    /// InProgress & mine on a crew job whose caller has marked their part done while someone else on it
    /// has not: nothing to do but wait for them.
    case awaitingCrew
    case complete
    /// InProgress & mine on a crew job everyone on it has marked done: the shared Complete, which any
    /// of them sends.
    case completeJob
    /// InProgress & mine but no "after" photo yet: the after photo is the move (the server's after-photos guard,
    /// surfaced early). `cashPending` carries the cash the photo gate is hiding, so the Finishing step can
    /// state the whole remaining sequence instead of only the photo.
    case completeBlocked(cashPending: Bool)
    /// Where Complete would stand on a card job Stripe has not charged (`CompleteOrder.CardIsCharged`).
    case paymentNotConfirmed
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
        case .takeOverlaps, .takeWaiting, .setOffLocked, .awaitingCrew, .completeBlocked, .paymentNotConfirmed,
             .none: nil
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
        crew: CrewFinish,
        mateMarked: Bool = false,
        isCardNotCharged: Bool = false,
        start: Date? = nil,
        end: Date? = nil,
        now: Date = Date(),
        clash: Clash? = nil,
        ownRowsPending: Bool = false
    ) -> OrderPrimaryAction {
        switch status {
        case ._0:
            // New: takeable by a non-assignee; a (rare) assigned New has no
            // action — the server lifecycle should have moved it to Confirmed.
            return isMine ? .none : take(clash: clash, ownRowsPending: ownRowsPending)
        case ._2:
            // Confirmed: takeable by a non-assignee; the assignee sets off once the server lets them.
            guard isMine else { return take(clash: clash, ownRowsPending: ownRowsPending) }
            return OrdersBoardRules.setOffLock(start: start, now: now).map { .setOffLocked(opensAt: $0) }
                ?? .notifyOnTheWay
        case ._3:
            // OnTheWay: the assignee starts. A NON-assignee can still take a seat — the status is on
            // the ORDER, not on a person, so one crew mate setting off used to remove the whole job
            // from every board with its other seats empty (owner ruling 2026-09-06). The server gate
            // is the authority; browse-detail only returns this order while a seat remains.
            if isMine { return .start }
            return joinable(end: end, now: now) ? take(clash: clash, ownRowsPending: ownRowsPending) : .none
        case ._4:
            // InProgress: only the assignee finishes. On a crew job the standing comes first (their
            // own "I'm done", then the wait); either Complete is then gated on an after-photo, then on
            // payment — a cash order settles only once the cleaner collects (the
            // `OrderPrimaryAction.kt` canComplete → needsCashCollection ordering).
            // Same as OnTheWay: the work has begun short-crewed and a seat is open. A late joiner
            // is worth more to the customer than an empty seat.
            guard isMine else {
                return joinable(end: end, now: now) ? take(clash: clash, ownRowsPending: ownRowsPending) : .none
            }
            return finishing(
                crew: crew,
                mateMarked: mateMarked,
                hasAfterPhotos: hasAfterPhotos,
                cashPending: isCashPayment && !isPaymentSettled,
                cardNotCharged: isCardNotCharged
            )
        case ._1, ._5, ._6, .none:
            // Pending / Completed / Cancelled / unknown — no action.
            return .none
        }
    }

    /// The span of her own job a job not taken would overlap.
    struct Clash: Equatable {
        let start: Date
        let end: Date
    }

    /// Her own jobs answer first: until they do Take waits, and once they do an overlap disables it. A read that
    /// failed leaves the clash to the server, as the board does.
    private static func take(clash: Clash?, ownRowsPending: Bool) -> OrderPrimaryAction {
        if ownRowsPending { return .takeWaiting }
        if let clash { return .takeOverlaps(start: clash.start, end: clash.end) }
        return .take
    }

    /// A late join is taken until the booked end (`OrderAvailability.IsOfferable`). The seat is the server's: showing
    /// her the job not hers already means one is takeable, a cover-requested one included.
    private static func joinable(end: Date?, now: Date) -> Bool {
        end.map { now < $0 } ?? true
    }

    /// Her open part of a crew job is cleaning as a solo job's is, the after photo first, until a crew-mate has
    /// marked; then I'm done whatever the photos. The payment stays the shared Complete's.
    private static func finishing(
        crew: CrewFinish,
        mateMarked: Bool,
        hasAfterPhotos: Bool,
        cashPending: Bool,
        cardNotCharged: Bool
    ) -> OrderPrimaryAction {
        switch crew {
        case .myPartOpen where mateMarked || hasAfterPhotos: return .markDone
        case .myPartOpen: return .completeBlocked(cashPending: cashPending)
        case .waitingForCrew: return .awaitingCrew
        case .solo, .everyoneDone: break
        }
        guard hasAfterPhotos else { return .completeBlocked(cashPending: cashPending) }
        if cashPending { return .collectCash }
        if cardNotCharged { return .paymentNotConfirmed }
        return crew == .everyoneDone ? .completeJob : .complete
    }
}

extension OrderPrimaryAction {
    /// The move of the page drawn from the board's row, nil where only its slot can be held. While the job loads the
    /// row tells the locked set-off and a job not hers, whose Take waits for her rows; any other move of hers waits
    /// for the job. A failed load pins the locked set-off alone, since nothing would resolve anything else.
    static func fromRow(_ order: OrderDetail, loading: Bool, now: Date) -> OrderPrimaryAction? {
        if order.isAssignedToCurrentUser, order.status == ._2,
           let opensAt = OrdersBoardRules.setOffLock(start: order.cleaningDateTime, now: now)
        {
            return .setOffLocked(opensAt: opensAt)
        }
        guard loading else { return OrderPrimaryAction.none }
        guard order.isAssignedToCurrentUser else {
            return action(
                for: order.status,
                isMine: false,
                hasAfterPhotos: false,
                isCashPayment: false,
                isPaymentSettled: false,
                crew: .solo,
                start: order.cleaningDateTime,
                end: order.bookedEnd,
                now: now,
                ownRowsPending: true
            )
        }
        return [._2, ._3, ._4].contains(order.status) ? nil : OrderPrimaryAction.none
    }
}

extension OrdersBoardRules {
    /// The locked slide's second line, on the board and the detail alike: the countdown when setting off opens
    /// today, else that day by its weekday and date.
    static func setOffWhen(_ opensAt: Date, now: Date, locale: Locale, calendar: Calendar = .current) -> String {
        guard calendar.isDate(opensAt, inSameDayAs: now) else {
            let weekday = OrdersFormat.wideWeekday(opensAt, locale: locale)
            return weekday + dot + OrdersFormat.dayMonth(opensAt, locale: locale)
        }
        let minutes = Int((opensAt.timeIntervalSince(now) / 60).rounded(.up))
        return L10n.Orders.heroIn(durationText(minutes))
    }

    /// When setting off opens, while it is still locked; nil once it is open, or with no start to lock it to. The
    /// hero and the detail's pinned move both ask this.
    static func setOffLock(start: Date?, now: Date) -> Date? {
        guard let opensAt = start?.addingTimeInterval(-TimeInterval(startGraceWindowMinutes) * 60), now < opensAt
        else { return nil }
        return opensAt
    }

    /// The board's overlap rule for a window the detail holds rather than a row.
    static func clash(start: Date, end: Date, own: [OrderListItem]) -> OrderListItem? {
        own
            .filter { mine in
                guard let mineStart = mine.cleaningDateTime, let mineEnd = bookedEnd(mine) else { return false }
                return mineStart < end && mineEnd > start
            }
            .min { ($0.cleaningDateTime ?? .distantFuture) < ($1.cleaningDateTime ?? .distantFuture) }
    }
}
