import CleansiaCore
import XCTest
@testable import CleansiaPartner

@MainActor
final class NotificationBadgeModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client)
    }

    func testRefreshAppliesTheServerCount() async {
        client.unreadCountResult = .success(3)
        await badge.refresh()
        XCTAssertEqual(badge.unreadCount, 3)
    }

    func testRefreshFailureKeepsTheLastValueSilently() async {
        client.unreadCountResult = .success(4)
        await badge.refresh()
        client.unreadCountResult = .failure(ApiError(httpStatus: 401))
        await badge.refresh()
        XCTAssertEqual(badge.unreadCount, 4)
    }

    func testSignedOutFetchFailureLeavesZeroAndDoesNotCrash() async {
        client.unreadCountResult = .failure(ApiError(httpStatus: 401))
        await badge.refresh()
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testPushReceiptIncrementsForAFeedEvent() {
        badge.notePushReceived(eventKey: "order.new_available")
        XCTAssertEqual(badge.unreadCount, 1)
        XCTAssertEqual(client.unreadCountCallCount, 0)
    }

    func testPushReceiptIgnoresCustomerAndUnknownKeys() {
        badge.notePushReceived(eventKey: "order.completed")
        badge.notePushReceived(eventKey: "promo.new_sitewide")
        badge.notePushReceived(eventKey: "totally.unknown")
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testNoteMarkedReadDecrementsAndClampsAtZero() {
        badge.notePushReceived(eventKey: "order.new_available")
        badge.noteMarkedRead()
        badge.noteMarkedRead()
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testLabelCapsAt99Plus() {
        XCTAssertNil(NotificationBadgeModel.label(for: 0))
        XCTAssertEqual(NotificationBadgeModel.label(for: 1), "1")
        XCTAssertEqual(NotificationBadgeModel.label(for: 99), "99")
        XCTAssertEqual(NotificationBadgeModel.label(for: 100), "99+")
    }

    func testClearWipesTheCount() async {
        client.unreadCountResult = .success(7)
        await badge.refresh()
        await badge.clear()
        XCTAssertEqual(badge.unreadCount, 0)
    }

    /// A count still in flight at sign-out answered into the next session and showed the previous cleaner's
    /// unread count on the next one's bell (S11).
    func testAnUnreadCountHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "unread count entered")
        client.holdUnreadCount = true
        client.unreadCountStarted = { _ in entered.fulfill() }
        let refresh = Task { await badge.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await badge.clear()
        client.releaseUnreadCount(call: 1, result: .success(7))
        await refresh.value

        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testANewSessionsUnreadCountIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old unread count entered")
        let newEntered = expectation(description: "new unread count entered")
        client.holdUnreadCount = true
        client.unreadCountStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let old = Task { await badge.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await badge.clear()

        let new = Task { await badge.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseUnreadCount(call: 2, result: .success(2))
        await new.value
        client.releaseUnreadCount(call: 1, result: .success(7))
        await old.value

        XCTAssertEqual(client.unreadCountCallCount, 2)
        XCTAssertEqual(badge.unreadCount, 2)
    }
}
