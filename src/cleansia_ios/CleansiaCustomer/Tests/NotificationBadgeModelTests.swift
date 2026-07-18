import CleansiaCore
import XCTest
@testable import CleansiaCustomer

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
        XCTAssertEqual(badge.badgeLabel, "3")
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
        XCTAssertNil(badge.badgeLabel)
    }

    func testPushReceiptIncrementsForAFeedEvent() {
        badge.notePushReceived(eventKey: "order.completed")
        XCTAssertEqual(badge.unreadCount, 1)
        XCTAssertEqual(client.unreadCountCallCount, 0)
    }

    func testPushReceiptIgnoresPromoAndUnknownKeys() {
        badge.notePushReceived(eventKey: "promo.new_sitewide")
        badge.notePushReceived(eventKey: "totally.unknown")
        badge.notePushReceived(eventKey: "order.new_available")
        XCTAssertEqual(badge.unreadCount, 0)
    }

    func testNoteMarkedReadDecrementsAndClampsAtZero() {
        badge.notePushReceived(eventKey: "order.completed")
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
}
