import XCTest
@testable import CleansiaPartner

final class PartnerNotificationDeepLinkTests: XCTestCase {
    func testOrderEventWithIdResolvesToOrderDestination() {
        let destination = PartnerNotificationDeepLink.resolve(eventKey: "order.confirmed", orderId: "ord-1")
        XCTAssertEqual(destination, .order(orderId: "ord-1"))
    }

    func testAllOrderScopedEventsResolveToOrder() {
        let keys = [
            "order.confirmed",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.on_the_way",
            "dispute.reply"
        ]
        for key in keys {
            XCTAssertEqual(
                PartnerNotificationDeepLink.resolve(eventKey: key, orderId: "ord-1"),
                .order(orderId: "ord-1"),
                key
            )
        }
    }

    func testOrderEventWithoutIdResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: "order.confirmed", orderId: nil))
    }

    func testNewAvailableResolvesToOrdersTab() {
        XCTAssertEqual(
            PartnerNotificationDeepLink.resolve(eventKey: "order.new_available", orderId: nil),
            .ordersTab
        )
    }

    func testUnknownEventResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(eventKey: "loyalty.points", orderId: "ord-1"))
    }

    func testResolveFromUserInfoMapsEventKeyAndOrderId() {
        let userInfo: [AnyHashable: Any] = ["event_key": "order.completed", "orderId": "ord-9"]
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-9"))
    }

    func testResolveFromUserInfoWithEmptyOrderIdResolvesToNil() {
        let userInfo: [AnyHashable: Any] = ["event_key": "order.completed", "orderId": ""]
        XCTAssertNil(PartnerNotificationDeepLink.resolve(userInfo))
    }

    func testResolveFromUserInfoWithoutEventKeyResolvesToNil() {
        XCTAssertNil(PartnerNotificationDeepLink.resolve(["orderId": "ord-1"]))
    }

    // The APNs display payload adds an `aps` alert block alongside the data
    // keys; resolution must read only the data keys and stay unaffected.

    func testResolveFromAlertCarryingUserInfoStillResolvesOrder() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "order.confirmed",
            locArgs: ["A-1042"],
            extra: ["orderId": "ord-1"]
        )
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .order(orderId: "ord-1"))
    }

    func testResolveFromAlertCarryingUserInfoStillResolvesOrdersTab() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "order.new_available",
            locArgs: ["3"],
            extra: ["count": "3"]
        )
        XCTAssertEqual(PartnerNotificationDeepLink.resolve(userInfo), .ordersTab)
    }

    func testResolveFromAlertCarryingUserInfoWithUnknownEventStillResolvesToNil() {
        let userInfo = alertCarryingUserInfo(
            eventKey: "promo.new_sitewide",
            locArgs: [],
            extra: ["orderId": "ord-1"]
        )
        XCTAssertNil(PartnerNotificationDeepLink.resolve(userInfo))
    }

    private func alertCarryingUserInfo(
        eventKey: String,
        locArgs: [String],
        extra: [AnyHashable: Any]
    ) -> [AnyHashable: Any] {
        var userInfo: [AnyHashable: Any] = [
            "aps": [
                "alert": [
                    "title-loc-key": "push.\(eventKey).title",
                    "loc-key": "push.\(eventKey).body",
                    "loc-args": locArgs
                ],
                "sound": "default",
                "thread-id": (extra["orderId"] as? String) ?? eventKey
            ],
            "event_key": eventKey
        ]
        for (key, value) in extra {
            userInfo[key] = value
        }
        return userInfo
    }
}
