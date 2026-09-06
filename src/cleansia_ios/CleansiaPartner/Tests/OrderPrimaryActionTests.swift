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
            isPaymentSettled: settled
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

    // A NON-assignee can now TAKE a started job while a seat remains. OrderStatus is on the ORDER,
    // not on a person, so one crew mate tapping "on my way" used to remove the whole booking from
    // every board with its other seats empty — on a three-person job, two seats locked out by one
    // person's tap. Owner ruling 2026-09-06 made offerability mean "the work is not OVER" rather
    // than "has not STARTED". → OrderAvailability.OfferableStatuses
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

    // The after-photos gate belongs to the ASSIGNEE completing the job; it has nothing to say to a
    // cleaner who is joining one. A late joiner is worth more to the customer than an empty seat.
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

    // Cash collection is likewise the assignee's gate, not a bar on joining.
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
}
