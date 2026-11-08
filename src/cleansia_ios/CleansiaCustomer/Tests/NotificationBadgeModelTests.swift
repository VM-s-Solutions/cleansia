import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The customer's badge counts the customer's feed keys; the badge itself is tested in Core.
@MainActor
final class NotificationBadgeModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client, feedEventKeys: CustomerFeedEventKeys.all)
    }

    func testPushReceiptIncrementsForAFeedEvent() {
        badge.notePushReceived(eventKey: "order.completed")
        XCTAssertEqual(badge.unreadCount, 1)
        XCTAssertEqual(client.unreadCountCallCount, 0)
    }

    func testPushReceiptIncrementsForCleanerAssigned() {
        badge.notePushReceived(eventKey: "order.cleaner_assigned")
        XCTAssertEqual(badge.unreadCount, 1)
    }

    func testPushReceiptIgnoresPromoAndUnknownKeys() {
        badge.notePushReceived(eventKey: "promo.new_sitewide")
        badge.notePushReceived(eventKey: "totally.unknown")
        badge.notePushReceived(eventKey: "order.new_available")
        XCTAssertEqual(badge.unreadCount, 0)
    }
}
