import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderPrimaryActionTests: XCTestCase {
    private func action(
        _ status: OrderStatus?,
        mine: Bool,
        photos: Bool = false,
        cash: Bool = false,
        settled: Bool = false
    ) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: status,
            isMine: mine,
            hasAfterPhotos: photos,
            isCashPayment: cash,
            isPaymentSettled: settled,
            crew: .solo
        )
    }

    // MARK: New (0)

    func testNewNotMineIsTake() {
        XCTAssertEqual(action(._0, mine: false), .take)
    }

    func testNewMineIsNone() {
        XCTAssertEqual(action(._0, mine: true), .none)
    }

    // MARK: Confirmed (2)

    func testConfirmedNotMineIsTake() {
        XCTAssertEqual(action(._2, mine: false), .take)
    }

    func testConfirmedMineIsNotifyOnTheWay() {
        XCTAssertEqual(action(._2, mine: true), .notifyOnTheWay)
    }

    // MARK: OnTheWay (3)

    func testOnTheWayMineIsStart() {
        XCTAssertEqual(action(._3, mine: true), .start)
    }

    /// A NON-assignee can now TAKE a started job while a seat remains. OrderStatus is on the ORDER,
    /// not on a person, so one crew mate tapping "on my way" used to remove the whole booking from
    /// every board with its other seats empty — on a three-person job, two seats locked out by one
    /// person's tap. Owner ruling 2026-09-06 made offerability mean "the work is not OVER" rather
    /// than "has not STARTED". → OrderAvailability.OfferableStatuses
    func testOnTheWayNotMineIsTake() {
        XCTAssertEqual(action(._3, mine: false), .take)
    }

    // MARK: InProgress (4) — after-photos gate

    func testInProgressMineWithAfterPhotosIsComplete() {
        XCTAssertEqual(action(._4, mine: true, photos: true), .complete)
    }

    func testInProgressMineWithoutAfterPhotosIsCompleteBlocked() {
        XCTAssertEqual(action(._4, mine: true, photos: false), .completeBlocked(cashPending: false))
    }

    func testCardOrderIsNeverBlockedWithCashPending() {
        XCTAssertEqual(
            action(._4, mine: true, photos: false, cash: false, settled: false),
            .completeBlocked(cashPending: false)
        )
    }

    /// The after-photos gate belongs to the ASSIGNEE completing the job; it has nothing to say to a
    /// cleaner who is joining one. A late joiner is worth more to the customer than an empty seat.
    func testInProgressNotMineIsTakeRegardlessOfPhotos() {
        XCTAssertEqual(action(._4, mine: false, photos: true), .take)
        XCTAssertEqual(action(._4, mine: false, photos: false), .take)
    }

    // MARK: InProgress (4) — cash-collection gate (after the after-photos gate)

    func testInProgressMineUnsettledCashWithAfterPhotosIsCollectCash() {
        XCTAssertEqual(action(._4, mine: true, photos: true, cash: true, settled: false), .collectCash)
    }

    func testInProgressMineUnsettledCashWithoutAfterPhotosIsStillCompleteBlocked() {
        // The after-photos gate is checked first (the Android canComplete →
        // needsCashCollection ordering), so no photo blocks before cash shows —
        // but the blocked case carries the still-owed cash so the hint can
        // spell out the whole remaining sequence.
        XCTAssertEqual(
            action(._4, mine: true, photos: false, cash: true, settled: false),
            .completeBlocked(cashPending: true)
        )
    }

    func testInProgressMineSettledCashResolvesToTheAfterPhotosGate() {
        XCTAssertEqual(action(._4, mine: true, photos: true, cash: true, settled: true), .complete)
        XCTAssertEqual(
            action(._4, mine: true, photos: false, cash: true, settled: true),
            .completeBlocked(cashPending: false)
        )
    }

    func testInProgressMineCardOrderNeverCollectsCash() {
        XCTAssertEqual(action(._4, mine: true, photos: true, cash: false, settled: false), .complete)
    }

    /// Cash collection is likewise the assignee's gate, not a bar on joining.
    func testInProgressNotMineUnsettledCashIsStillTake() {
        XCTAssertEqual(action(._4, mine: false, photos: true, cash: true, settled: false), .take)
    }

    // MARK: Pending (1) / Completed (5) / Cancelled (6) / nil — terminal/no-op

    func testPendingIsAlwaysNone() {
        XCTAssertEqual(action(._1, mine: true), .none)
        XCTAssertEqual(action(._1, mine: false), .none)
    }

    func testCompletedIsAlwaysNone() {
        XCTAssertEqual(action(._5, mine: true, photos: true), .none)
        XCTAssertEqual(action(._5, mine: false), .none)
    }

    func testCancelledIsAlwaysNone() {
        XCTAssertEqual(action(._6, mine: true, photos: true), .none)
        XCTAssertEqual(action(._6, mine: false), .none)
    }

    func testNilStatusIsNone() {
        XCTAssertEqual(action(nil, mine: true, photos: true), .none)
        XCTAssertEqual(action(nil, mine: false), .none)
    }

    // MARK: orderAction discriminator

    func testOrderActionDiscriminatorMapping() {
        XCTAssertEqual(OrderPrimaryAction.take.orderAction, .take)
        XCTAssertEqual(OrderPrimaryAction.notifyOnTheWay.orderAction, .notifyOnTheWay)
        XCTAssertEqual(OrderPrimaryAction.start.orderAction, .start)
        XCTAssertEqual(OrderPrimaryAction.collectCash.orderAction, .markCashCollected)
        XCTAssertEqual(OrderPrimaryAction.complete.orderAction, .complete)
        XCTAssertNil(OrderPrimaryAction.completeBlocked(cashPending: false).orderAction)
        XCTAssertNil(OrderPrimaryAction.completeBlocked(cashPending: true).orderAction)
        XCTAssertNil(OrderPrimaryAction.none.orderAction)
    }

    // MARK: The pinned move never offers what the server refuses

    private let start = Date(timeIntervalSince1970: 1_786_000_000)

    private func confirmed(at minutesFromStart: Double) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: ._2,
            isMine: true,
            hasAfterPhotos: false,
            isCashPayment: false,
            isPaymentSettled: false,
            crew: .solo,
            start: start,
            now: start.addingTimeInterval(minutesFromStart * 60)
        )
    }

    /// The server refuses "on the way" before the start minus an hour: the slide stays locked until then, and stays
    /// open once the start has passed: she can still set off late.
    func testSettingOffIsLockedUntilAnHourBeforeTheStart() {
        let opensAt = start.addingTimeInterval(-3600)
        XCTAssertEqual(confirmed(at: -280), .setOffLocked(opensAt: opensAt))
        XCTAssertEqual(confirmed(at: -61), .setOffLocked(opensAt: opensAt))
        XCTAssertEqual(confirmed(at: -60), .notifyOnTheWay)
        XCTAssertEqual(confirmed(at: 10), .notifyOnTheWay)
        XCTAssertNil(OrderPrimaryAction.setOffLocked(opensAt: opensAt).orderAction)
    }

    func testTheBoardsHeroAndTheDetailLockTheSameMinute() {
        var row = OrderListItem.sample(id: "A1", status: ._2, cleaningDateTime: start)
        row.estimatedTime = 120
        for minutes in [-280.0, -61, -60, 10] {
            let now = start.addingTimeInterval(minutes * 60)
            let heroLocked = OrdersBoardRules.heroControl(row, now: now) == .setOff(
                opensAt: start.addingTimeInterval(-3600),
                enabled: false
            )
            XCTAssertEqual(heroLocked, confirmed(at: minutes) != .notifyOnTheWay, "\(minutes) min")
            XCTAssertEqual(OrdersBoardRules.setOffLock(start: start, now: now) != nil, heroLocked, "\(minutes) min")
        }
    }

    private func take(clash: OrderPrimaryAction.Clash? = nil, pending: Bool = false) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: ._0,
            isMine: false,
            hasAfterPhotos: false,
            isCashPayment: false,
            isPaymentSettled: false,
            crew: .solo,
            clash: clash,
            ownRowsPending: pending
        )
    }

    /// Take waits for her own jobs, so a tap cannot reach the server before the clash check; a clash disables it.
    func testTakeWaitsForHerJobsAndIsRefusedOnAnOverlap() {
        let clash = OrderPrimaryAction.Clash(start: start, end: start.addingTimeInterval(10800))
        XCTAssertEqual(take(pending: true), .takeWaiting)
        XCTAssertEqual(take(clash: clash, pending: true), .takeWaiting)
        XCTAssertEqual(take(clash: clash), .takeOverlaps(start: clash.start, end: clash.end))
        XCTAssertEqual(take(), .take, "a failed read offers Take and leaves the clash to the server")
        XCTAssertNil(OrderPrimaryAction.takeWaiting.orderAction)
        XCTAssertNil(OrderPrimaryAction.takeOverlaps(start: start, end: start).orderAction)
    }

    private func finishing(crew: CrewFinish, cash: Bool = false, cardNotCharged: Bool) -> OrderPrimaryAction {
        OrderPrimaryAction.action(
            for: ._4,
            isMine: true,
            hasAfterPhotos: true,
            isCashPayment: cash,
            isPaymentSettled: false,
            crew: crew,
            isCardNotCharged: cardNotCharged
        )
    }

    /// `CompleteOrder` refuses a card job Stripe has not charged: where Complete would stand, the locked slide.
    func testACardStripeHasNotChargedLocksComplete() {
        XCTAssertEqual(finishing(crew: .solo, cardNotCharged: true), .paymentNotConfirmed)
        XCTAssertEqual(finishing(crew: .everyoneDone, cardNotCharged: true), .paymentNotConfirmed)
        XCTAssertEqual(finishing(crew: .solo, cardNotCharged: false), .complete)
        XCTAssertEqual(finishing(crew: .myPartOpen, cardNotCharged: true), .markDone, "her mark has no payment rule")
        XCTAssertEqual(finishing(crew: .solo, cash: true, cardNotCharged: false), .collectCash)
        XCTAssertNil(OrderPrimaryAction.paymentNotConfirmed.orderAction)
    }

    /// A late join is taken until the booked end, as `OrderAvailability.IsOfferable` takes it; after it nothing is
    /// pinned. The seat itself is the server's: showing her the job not hers already means one is takeable.
    func testALateJoinIsOfferedUntilTheBookedEnd() {
        let end = start.addingTimeInterval(7200)
        for status in [OrderStatus._3, ._4] {
            let join = { (now: Date) in
                OrderPrimaryAction.action(
                    for: status,
                    isMine: false,
                    hasAfterPhotos: false,
                    isCashPayment: false,
                    isPaymentSettled: false,
                    crew: .solo,
                    start: self.start,
                    end: end,
                    now: now
                )
            }
            XCTAssertEqual(join(start.addingTimeInterval(600)), .take, "\(status)")
            XCTAssertEqual(join(end.addingTimeInterval(-60)), .take, "\(status)")
            XCTAssertEqual(join(end), OrderPrimaryAction.none, "\(status)")
            XCTAssertEqual(join(end.addingTimeInterval(600)), OrderPrimaryAction.none, "\(status)")
        }
    }

    private func rowDetail(status: OrderStatus, mine: Bool, start: Date) throws -> OrderDetail {
        var row = OrderListItem.sample(id: "A1", status: status, cleaningDateTime: start)
        row.estimatedTime = 120
        return try XCTUnwrap(OrderDetail(row: row, mine: mine))
    }

    /// The page drawn from the board's row pins only what the row can tell. While the job loads: the locked set-off,
    /// a waiting Take on a job not hers, and for any other move of hers only its slot (nil). After a failed load: the
    /// locked set-off alone, nothing else and no slot.
    func testThePageDrawnFromTheRowPinsOnlyWhatTheRowCanTell() throws {
        let opensAt = start.addingTimeInterval(-3600)
        let early = start.addingTimeInterval(-7200)
        let open = start.addingTimeInterval(-600)
        let locked = try rowDetail(status: ._2, mine: true, start: start)
        XCTAssertEqual(OrderPrimaryAction.fromRow(locked, loading: true, now: early), .setOffLocked(opensAt: opensAt))
        XCTAssertEqual(OrderPrimaryAction.fromRow(locked, loading: false, now: early), .setOffLocked(opensAt: opensAt))
        for status in [OrderStatus._2, ._3, ._4] {
            let hers = try rowDetail(status: status, mine: true, start: start)
            XCTAssertNil(OrderPrimaryAction.fromRow(hers, loading: true, now: open), "\(status)")
            XCTAssertEqual(OrderPrimaryAction.fromRow(hers, loading: false, now: open), OrderPrimaryAction.none)
        }
        for status in [OrderStatus._0, ._2, ._3, ._4] {
            let notHers = try rowDetail(status: status, mine: false, start: start)
            XCTAssertEqual(OrderPrimaryAction.fromRow(notHers, loading: true, now: open), .takeWaiting, "\(status)")
            XCTAssertEqual(OrderPrimaryAction.fromRow(notHers, loading: false, now: open), OrderPrimaryAction.none)
        }
    }
}
