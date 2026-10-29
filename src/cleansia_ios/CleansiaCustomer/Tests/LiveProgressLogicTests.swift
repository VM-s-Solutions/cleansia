import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

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

    func testInProgressFractionFromStartedEntry() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(30 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 0.5, accuracy: 0.001)
    }

    func testElapsedEstimateFillsWithoutClaimingACompletedPhase() {
        let started = Date(timeIntervalSince1970: 1000)
        let now = started.addingTimeInterval(10 * 60 * 60)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: started)]
        let fraction = LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now)
        XCTAssertEqual(fraction ?? 0, 1, accuracy: 0.001)
        XCTAssertEqual(LiveProgress.activeStep(for: ._4), .started)
    }

    func testFutureStartDoesNotProduceAnInventedProgress() {
        let now = Date(timeIntervalSince1970: 1000)
        XCTAssertNil(LiveProgress.inProgressFraction(
            history: [OrderFixtures.track(statusValue: 4, createdOn: now.addingTimeInterval(60))],
            estimatedMinutes: 60,
            now: now
        ))
    }

    func testOverrunReportsElapsedTimeRatherThanRemainingAtNinetySevenPercent() throws {
        let now = Date(timeIntervalSince1970: 10000)
        let history = [OrderFixtures.track(statusValue: 4, createdOn: now.addingTimeInterval(-3661))]
        let progress = try XCTUnwrap(LiveProgress.estimateProgress(history: history, estimatedMinutes: 60, now: now))
        XCTAssertEqual(progress.fraction, 1)
        XCTAssertEqual(progress.overrunMinutes, 2)
        XCTAssertEqual(progress.remainingMinutes, 0)
    }

    func testLatestRecordedCleaningStartControlsTheEstimate() {
        let now = Date(timeIntervalSince1970: 10000)
        let history = [
            OrderFixtures.track(statusValue: 4, createdOn: now.addingTimeInterval(-3600)),
            OrderFixtures.track(statusValue: 4, createdOn: now.addingTimeInterval(-1800))
        ]
        XCTAssertEqual(LiveProgress.inProgressFraction(history: history, estimatedMinutes: 60, now: now), 0.5)
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
