import XCTest
@testable import CleansiaPartner

@MainActor
final class OrdersStalenessTests: XCTestCase {
    private var clock = Date(timeIntervalSince1970: 1000)

    private func makeCache() -> OrdersStaleness {
        OrdersStaleness(window: 30, now: { self.clock })
    }

    func testUnmarkedPaneIsStale() {
        XCTAssertTrue(makeCache().isPaneStale(.available))
    }

    func testFreshlyMarkedPaneIsNotStale() {
        let cache = makeCache()
        cache.markPaneFresh(.available)
        XCTAssertFalse(cache.isPaneStale(.available))
    }

    func testPaneGoesStaleAfterWindow() {
        let cache = makeCache()
        cache.markPaneFresh(.active)
        clock = clock.addingTimeInterval(29)
        XCTAssertFalse(cache.isPaneStale(.active))
        clock = clock.addingTimeInterval(1)
        XCTAssertTrue(cache.isPaneStale(.active))
    }

    func testMarkingOnePaneDoesNotFreshenAnother() {
        let cache = makeCache()
        cache.markPaneFresh(.available)
        XCTAssertFalse(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertTrue(cache.isPaneStale(.history))
    }

    func testPerOrderFreshness() {
        let cache = makeCache()
        cache.markOrderFresh("o1")
        XCTAssertFalse(cache.isOrderStale("o1"))
        XCTAssertTrue(cache.isOrderStale("o2"))
        cache.invalidateOrder("o1")
        XCTAssertTrue(cache.isOrderStale("o1"))
    }

    func testTakeInvalidatesAvailableAndActiveOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .takeOrder)
        XCTAssertTrue(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertFalse(cache.isPaneStale(.history))
    }

    func testNotifyInvalidatesAvailableAndActiveOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .notifyOnTheWay)
        XCTAssertTrue(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertFalse(cache.isPaneStale(.history))
    }

    func testStartInvalidatesActiveOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .startOrder)
        XCTAssertFalse(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertFalse(cache.isPaneStale(.history))
    }

    /// The standalone acceptance changes nothing about which board a job is on — only the caller's
    /// own row on a job they already hold.
    func testAcceptWorkContractInvalidatesActiveOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .acceptWorkContract)
        XCTAssertFalse(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertFalse(cache.isPaneStale(.history))
    }

    /// The job leaves the cleaner's own list and its seat is open on the board again.
    func testDropInvalidatesAvailableAndActiveOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .dropOrder)
        XCTAssertTrue(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertFalse(cache.isPaneStale(.history))
    }

    func testCompleteInvalidatesActiveAndHistoryOnly() {
        let cache = freshAllPanes()
        cache.invalidatePanes(for: .completeOrder)
        XCTAssertFalse(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isPaneStale(.active))
        XCTAssertTrue(cache.isPaneStale(.history))
    }

    func testClearResetsEverything() async {
        let cache = freshAllPanes()
        cache.markOrderFresh("o1")
        await cache.clear()
        XCTAssertTrue(cache.isPaneStale(.available))
        XCTAssertTrue(cache.isOrderStale("o1"))
    }

    private func freshAllPanes() -> OrdersStaleness {
        let cache = makeCache()
        for pane in OrdersPane.allCases {
            cache.markPaneFresh(pane)
        }
        return cache
    }

    func testDashboardHasItsOwnMinuteWithoutChangingPaneWindows() {
        let cache = makeCache()
        cache.markDashboardFresh()
        cache.markPaneFresh(.available)
        clock.addTimeInterval(30)
        XCTAssertFalse(cache.isDashboardStale)
        XCTAssertTrue(cache.isPaneStale(.available))
        clock.addTimeInterval(30)
        XCTAssertTrue(cache.isDashboardStale)
    }

    func testDashboardMutationRevisionAndSessionEpochHaveSeparateLifetimes() async {
        let cache = makeCache()
        let epoch = cache.sessionEpoch
        let revision = cache.dashboardRevision
        cache.markDashboardFresh()
        cache.invalidateDashboard()
        XCTAssertTrue(cache.isDashboardStale)
        XCTAssertEqual(cache.dashboardRevision, revision + 1)
        XCTAssertEqual(cache.sessionEpoch, epoch)
        cache.markDashboardFresh()
        await cache.clear()
        XCTAssertTrue(cache.isDashboardStale)
        XCTAssertEqual(cache.dashboardRevision, revision + 2)
        XCTAssertEqual(cache.sessionEpoch, epoch + 1)
    }
}
