import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class OrderStatusLogicTests: XCTestCase {
    func testSevenStateRawValuesMatchBackendInts() {
        // OrderEnums.kt: New=0, Pending=1, Confirmed=2, OnTheWay=3,
        // InProgress=4, Completed=5, Cancelled=6.
        XCTAssertEqual(OrderStatus._0.rawValue, 0)
        XCTAssertEqual(OrderStatus._1.rawValue, 1)
        XCTAssertEqual(OrderStatus._2.rawValue, 2)
        XCTAssertEqual(OrderStatus._3.rawValue, 3)
        XCTAssertEqual(OrderStatus._4.rawValue, 4)
        XCTAssertEqual(OrderStatus._5.rawValue, 5)
        XCTAssertEqual(OrderStatus._6.rawValue, 6)
    }

    func testCodeMapsToOrderStatusByValue() {
        let code = Code(type: "OrderStatus", name: "OnTheWay", value: 3)
        XCTAssertEqual(code.toOrderStatus(), ._3)
    }

    func testCodeWithNilValueMapsToNil() {
        XCTAssertNil(Code(type: nil, name: "OnTheWay", value: nil).toOrderStatus())
    }

    func testCodeWithUnknownValueMapsToNil() {
        XCTAssertNil(Code(type: nil, name: nil, value: 99).toOrderStatus())
    }

    func testActiveStatusesAreConfirmedOnTheWayInProgress() {
        XCTAssertTrue(OrderStatusGroup.isActive(._2))
        XCTAssertTrue(OrderStatusGroup.isActive(._3))
        XCTAssertTrue(OrderStatusGroup.isActive(._4))
        XCTAssertFalse(OrderStatusGroup.isActive(._0))
        XCTAssertFalse(OrderStatusGroup.isActive(._1))
        XCTAssertFalse(OrderStatusGroup.isActive(._5))
        XCTAssertFalse(OrderStatusGroup.isActive(._6))
    }

    func testUpcomingExcludesCompletedAndCancelled() {
        XCTAssertTrue(OrderStatusGroup.isUpcoming(._0))
        XCTAssertTrue(OrderStatusGroup.isUpcoming(._3))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(._5))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(._6))
        XCTAssertFalse(OrderStatusGroup.isUpcoming(nil))
    }

    func testCancellableIsNewPendingConfirmed() {
        XCTAssertTrue(OrderStatusGroup.isCancellable(._0))
        XCTAssertTrue(OrderStatusGroup.isCancellable(._1))
        XCTAssertTrue(OrderStatusGroup.isCancellable(._2))
        XCTAssertFalse(OrderStatusGroup.isCancellable(._3))
        XCTAssertFalse(OrderStatusGroup.isCancellable(._5))
    }
}

final class LiveProgressLogicTests: XCTestCase {
    func testActiveStepMapsEachOfSevenStates() {
        XCTAssertEqual(LiveProgress.activeStep(for: ._0), .booked)
        XCTAssertEqual(LiveProgress.activeStep(for: ._1), .booked)
        XCTAssertEqual(LiveProgress.activeStep(for: ._2), .accepted)
        XCTAssertEqual(LiveProgress.activeStep(for: ._3), .onTheWay)
        XCTAssertEqual(LiveProgress.activeStep(for: ._4), .started)
        XCTAssertEqual(LiveProgress.activeStep(for: ._5), .finished)
        XCTAssertNil(LiveProgress.activeStep(for: ._6))
        XCTAssertNil(LiveProgress.activeStep(for: nil))
    }

    func testOnTheWayIsADistinctStepNotFoldedIntoInProgress() {
        XCTAssertEqual(LiveProgress.activeStep(for: ._3), .onTheWay)
        XCTAssertNotEqual(LiveProgress.activeStep(for: ._3), LiveProgress.activeStep(for: ._4))
        XCTAssertEqual(LiveProgressStep.allCases.count, 5)
    }

    func testUsesLiveHeroOnlyForActiveStates() {
        XCTAssertTrue(LiveProgress.usesLiveHero(._2))
        XCTAssertTrue(LiveProgress.usesLiveHero(._3))
        XCTAssertTrue(LiveProgress.usesLiveHero(._4))
        XCTAssertFalse(LiveProgress.usesLiveHero(._5))
        XCTAssertFalse(LiveProgress.usesLiveHero(._0))
    }

    func testInProgressFractionFromStartedEntry() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(30 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 0.5, accuracy: 0.001)
    }

    func testInProgressFractionCapsAtNinetySeven() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(10 * 60 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 0.97, accuracy: 0.001)
    }

    func testInProgressFractionNilWithoutAnchors() {
        XCTAssertNil(LiveProgress.inProgressFraction(history: [], estimatedMinutes: 60, now: Date()))
        XCTAssertNil(LiveProgress.inProgressFraction(
            history: [OrderFixtures.track(statusValue: 4, createdOn: Date())],
            estimatedMinutes: 0,
            now: Date()
        ))
    }
}

final class CancellationFeePreviewTests: XCTestCase {
    private let base = Date(timeIntervalSince1970: 1_000_000)

    func testOopsWindowWithinFifteenMinutesOfBooking() {
        let tier = CancellationFeePreview.tier(
            cleaningAt: base.addingTimeInterval(2 * 3600),
            createdAt: base.addingTimeInterval(-10 * 60),
            totalPrice: 1000,
            now: base
        )
        XCTAssertEqual(tier, .oops)
    }

    func testFreeWhenAtLeast24HoursOut() {
        let tier = CancellationFeePreview.tier(
            cleaningAt: base.addingTimeInterval(30 * 3600),
            createdAt: base.addingTimeInterval(-2 * 3600),
            totalPrice: 1000,
            now: base
        )
        XCTAssertEqual(tier, .free)
    }

    func testHalfFeeBetweenFourAndTwentyFourHours() {
        let tier = CancellationFeePreview.tier(
            cleaningAt: base.addingTimeInterval(10 * 3600),
            createdAt: base.addingTimeInterval(-2 * 3600),
            totalPrice: 1000,
            now: base
        )
        XCTAssertEqual(tier, .half(refund: 500))
    }

    func testFullFeeUnderFourHours() {
        let tier = CancellationFeePreview.tier(
            cleaningAt: base.addingTimeInterval(2 * 3600),
            createdAt: base.addingTimeInterval(-2 * 3600),
            totalPrice: 1000,
            now: base
        )
        XCTAssertEqual(tier, .full)
    }

    func testNeutralWhenTimestampsMissing() {
        XCTAssertEqual(
            CancellationFeePreview.tier(cleaningAt: nil, createdAt: base, totalPrice: 1000, now: base),
            .neutral
        )
    }
}
