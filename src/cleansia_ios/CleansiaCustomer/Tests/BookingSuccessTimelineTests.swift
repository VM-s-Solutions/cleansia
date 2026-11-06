import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class BookingSuccessTimelineTests: XCTestCase {
    private func states(
        _ status: OrderStatus?,
        cleanerAssigned: Bool = false
    ) -> [BookingSuccessStepState] {
        BookingSuccessTimeline.entries(status: status, cleanerAssigned: cleanerAssigned).map(\.state)
    }

    func testFourStepsInAndroidOrder() {
        let entries = BookingSuccessTimeline.entries(status: nil, cleanerAssigned: false)

        XCTAssertEqual(entries.map(\.step), [.received, .assigning, .confirmed, .cleaningDay])
    }

    func testNoOrderLoadedShowsOnlyTheReceivedAcknowledgement() {
        XCTAssertEqual(states(nil), [.done, .pending, .pending, .pending])
    }

    func testNewAndPendingKeepAssignmentActiveUntilACleanerIsAssigned() {
        XCTAssertEqual(states(._0), [.done, .active, .pending, .pending])
        XCTAssertEqual(states(._1), [.done, .active, .pending, .pending])
        XCTAssertEqual(states(._0, cleanerAssigned: true), [.done, .done, .pending, .pending])
        XCTAssertEqual(states(._1, cleanerAssigned: true), [.done, .done, .pending, .pending])
    }

    func testConfirmedAndOnTheWayActivateTheConfirmationStep() {
        XCTAssertEqual(states(._2), [.done, .done, .active, .pending])
        XCTAssertEqual(states(._3), [.done, .done, .active, .pending])
    }

    func testInProgressActivatesCleaningDay() {
        XCTAssertEqual(states(._4), [.done, .done, .done, .active])
    }

    func testCompletedChecksEverythingOff() {
        XCTAssertEqual(states(._5), [.done, .done, .done, .done])
    }

    func testCancelledShowsNoActiveOrFalselyCompletedAssignment() {
        XCTAssertEqual(states(._6), [.done, .pending, .pending, .pending])
    }
}
