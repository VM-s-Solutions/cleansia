import XCTest
@testable import CleansiaCore

@MainActor
final class StalenessTests: XCTestCase {
    private var clock = Date(timeIntervalSince1970: 1_000_000)

    private func makeStaleness(window: TimeInterval = 30) -> Staleness {
        Staleness(window: window, now: { self.clock })
    }

    func testNeverFetchedReadsStale() {
        XCTAssertTrue(makeStaleness().isStale)
    }

    func testMarkFreshClearsStaleness() {
        let staleness = makeStaleness()
        staleness.markFresh()
        XCTAssertFalse(staleness.isStale)
    }

    func testStaysFreshInsideTheWindow() {
        let staleness = makeStaleness()
        staleness.markFresh()

        clock = clock.addingTimeInterval(29)

        XCTAssertFalse(staleness.isStale)
    }

    func testGoesStaleAtTheWindowBoundary() {
        let staleness = makeStaleness()
        staleness.markFresh()

        clock = clock.addingTimeInterval(30)

        XCTAssertTrue(staleness.isStale)
    }

    func testReMarkingSlidesTheWindowForward() {
        let staleness = makeStaleness()
        staleness.markFresh()

        clock = clock.addingTimeInterval(29)
        staleness.markFresh()
        clock = clock.addingTimeInterval(29)

        XCTAssertFalse(staleness.isStale)
    }

    func testInvalidateForcesTheNextCheckStale() {
        // Sign-out rides this: the next account must not read the previous
        // account's snapshot as fresh.
        let staleness = makeStaleness()
        staleness.markFresh()

        staleness.invalidate()

        XCTAssertTrue(staleness.isStale)
    }
}
