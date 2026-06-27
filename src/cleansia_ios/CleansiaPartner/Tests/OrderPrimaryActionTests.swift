import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrderPrimaryActionTests: XCTestCase {
    private func action(_ status: OrderStatus?, mine: Bool, photos: Bool = false) -> OrderPrimaryAction {
        OrderPrimaryAction.action(for: status, isMine: mine, hasAfterPhotos: photos)
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

    func testOnTheWayNotMineIsNone() {
        XCTAssertEqual(action(._3, mine: false), .none)
    }

    // MARK: InProgress (4) — after-photos gate

    func testInProgressMineWithAfterPhotosIsComplete() {
        XCTAssertEqual(action(._4, mine: true, photos: true), .complete)
    }

    func testInProgressMineWithoutAfterPhotosIsCompleteBlocked() {
        XCTAssertEqual(action(._4, mine: true, photos: false), .completeBlocked)
    }

    func testInProgressNotMineIsNoneRegardlessOfPhotos() {
        XCTAssertEqual(action(._4, mine: false, photos: true), .none)
        XCTAssertEqual(action(._4, mine: false, photos: false), .none)
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
        XCTAssertEqual(OrderPrimaryAction.complete.orderAction, .complete)
        XCTAssertNil(OrderPrimaryAction.completeBlocked.orderAction)
        XCTAssertNil(OrderPrimaryAction.none.orderAction)
    }
}
