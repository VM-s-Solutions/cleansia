import XCTest
@testable import CleansiaCore

final class LiveActivityPolicyTests: XCTestCase {
    private let now = Date(timeIntervalSince1970: 1_700_000_000)

    func testWireStatusesMatchTheBackendContentStateStrings() {
        XCTAssertEqual(LiveActivityTerminalStatus.completed.rawValue, "completed")
        XCTAssertEqual(LiveActivityTerminalStatus.cancelled.rawValue, "cancelled")
    }

    func testCancelledDismissesImmediatelyBecauseADeadOrderMustNotLinger() {
        XCTAssertEqual(LiveActivityPolicy.dismissal(for: .cancelled, now: now), .immediate)
    }

    func testCompletedLingersForTheGlanceWindow() {
        XCTAssertEqual(
            LiveActivityPolicy.dismissal(for: .completed, now: now),
            .after(now.addingTimeInterval(30 * 60))
        )
    }

    func testCompletedLingerIsThirtyMinutes() {
        XCTAssertEqual(LiveActivityPolicy.completedLinger, 30 * 60)
    }

    /// Mirrors `LiveActivityPayloadFactory.StaleDate`: a short booked clean still gets four hours of
    /// runway, so the card cannot render stale while the cleaner is still working.
    func testAShortCleanStillGetsTheFourHourFloor() {
        let staleDate = LiveActivityPolicy.staleDate(scheduledEnd: now.addingTimeInterval(60 * 60), now: now)

        XCTAssertEqual(staleDate, now.addingTimeInterval(4 * 3600))
    }

    func testALongCleanGoesStaleAnHourAfterItsBookedEnd() {
        let scheduledEnd = now.addingTimeInterval(6 * 3600)

        let staleDate = LiveActivityPolicy.staleDate(scheduledEnd: scheduledEnd, now: now)

        XCTAssertEqual(staleDate, scheduledEnd.addingTimeInterval(3600))
    }

    /// The bug this replaces: a stale date already behind "now" makes the system render the card as a
    /// placeholder immediately.
    func testAnAlreadyElapsedBookedEndStillYieldsAStaleDateInTheFuture() {
        let staleDate = LiveActivityPolicy.staleDate(scheduledEnd: now.addingTimeInterval(-5 * 3600), now: now)

        XCTAssertGreaterThan(staleDate, now)
        XCTAssertEqual(staleDate, now.addingTimeInterval(4 * 3600))
    }
}
