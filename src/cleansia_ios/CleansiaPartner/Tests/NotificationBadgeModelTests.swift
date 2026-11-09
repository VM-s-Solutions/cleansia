import CleansiaCore
import XCTest
@testable import CleansiaPartner

/// The partner's badge counts the partner's feed keys; the badge itself is tested in Core.
@MainActor
final class NotificationBadgeModelTests: XCTestCase {
    private var client: FakeNotificationFeedClient!
    private var badge: NotificationBadgeModel!

    override func setUp() {
        super.setUp()
        client = FakeNotificationFeedClient()
        badge = NotificationBadgeModel(client: client, feedEventKeys: PartnerFeedEventKeys.all)
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
}
